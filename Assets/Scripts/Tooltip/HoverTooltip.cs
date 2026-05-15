using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HoverTooltip : MonoBehaviour
{
    [Header("Tooltip Content")]
    public string message = "";

    [Header("Behaviour")]
    public float hoverDelay = 0.2f;
    public Vector2 tooltipOffset = new(0f, 60f);

    [Header("References")]
    public DragInputProvider inputProvider;
    public TooltipUI tooltipUI;

    protected Collider2D[] _colliders;
    protected Renderer _sortingRenderer;
    private float _hoverTimer;
    protected bool _isShowing;
    private Canvas _tooltipCanvas;
    private RectTransform _tooltipRect;

    private static readonly List<HoverTooltip> _instances = new();
    private static HoverTooltip _currentOwner;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (inputProvider == null)
            inputProvider = FindFirstObjectByType<DragInputProvider>();
        if (tooltipUI == null)
            tooltipUI = FindFirstObjectByType<TooltipUI>();
    }
#endif

    void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _sortingRenderer = GetComponentInChildren<Renderer>(true);
    }

    protected virtual void OnEnable()
    {
        _instances.Add(this);
    }

    void Start()
    {
        if (tooltipUI != null)
        {
            _tooltipCanvas = tooltipUI.GetComponentInParent<Canvas>();
            _tooltipRect = tooltipUI.GetComponent<RectTransform>();
            tooltipUI.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (inputProvider == null || tooltipUI == null) return;

        Vector3 pointerWorldPos = inputProvider.PointerWorldPosition;
        bool isHovering = AnyColliderOverlaps(pointerWorldPos);
        bool isHeld = inputProvider.IsHeld;

        if (isHovering && !isHeld)
        {
            if (IsTopmostHovered(pointerWorldPos))
            {
                _hoverTimer += Time.deltaTime;
                if (_hoverTimer >= hoverDelay && _currentOwner != this)
                {
                    _currentOwner = this;
                    _isShowing = true;
                    ShowTooltip();
                }
                if (_currentOwner == this)
                    PositionTooltip();
            }
            else
            {
                ReleaseOwnership();
                _hoverTimer = 0f;
            }
        }
        else
        {
            ReleaseOwnership();
            _hoverTimer = 0f;
        }
    }

    private bool IsTopmostHovered(Vector3 pointerWorldPos)
    {
        HoverTooltip topmost = null;
        int topmostLayerValue = int.MinValue;
        int topmostOrder = int.MinValue;
        int topmostID = int.MinValue;

        foreach (var instance in _instances)
        {
            if (!instance.AnyColliderOverlaps(pointerWorldPos)) continue;

            int layerValue = instance._sortingRenderer != null
                ? SortingLayer.GetLayerValueFromID(instance._sortingRenderer.sortingLayerID)
                : int.MinValue;
            int order = instance._sortingRenderer != null
                ? instance._sortingRenderer.sortingOrder
                : int.MinValue;
            int id = instance.GetInstanceID();

            if (layerValue > topmostLayerValue ||
                (layerValue == topmostLayerValue && order > topmostOrder) ||
                (layerValue == topmostLayerValue && order == topmostOrder && id > topmostID))
            {
                topmost = instance;
                topmostLayerValue = layerValue;
                topmostOrder = order;
                topmostID = id;
            }
        }

        return topmost == this;
    }

    private bool AnyColliderOverlaps(Vector2 worldPoint)
    {
        for (int i = 0; i < _colliders.Length; i++)
            if (_colliders[i].OverlapPoint(worldPoint)) return true;
        return false;
    }

    private void ReleaseOwnership()
    {
        if (_currentOwner == this)
        {
            _currentOwner = null;
            tooltipUI.gameObject.SetActive(false);
        }
        _isShowing = false;
    }

    protected virtual void ShowTooltip()
    {
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetText(message);
        PositionTooltip();
    }

    protected void PositionTooltip()
    {
        if (_tooltipCanvas == null || _tooltipRect == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        float canvasScale = _tooltipCanvas.scaleFactor;
        float tooltipW = _tooltipRect.rect.width * canvasScale;
        float tooltipH = _tooltipRect.rect.height * canvasScale;

        // Vertical: default pivot at bottom (grows upward); flip to top if it would exit the screen top
        float offsetY = tooltipOffset.y;
        float pivotY = 0f;
        if (screenPos.y + offsetY + tooltipH > Screen.height)
        {
            pivotY = 1f;
            offsetY = -tooltipOffset.y;
        }

        // Horizontal: keep existing logic (flip pivot side based on screen half)
        float pivotX = screenPos.x > Screen.width * 0.5f ? 1f : 0f;

        _tooltipRect.pivot = new Vector2(pivotX, pivotY);

        screenPos.x += tooltipOffset.x;
        screenPos.y += offsetY;

        // Clamp horizontally so tooltip stays within screen bounds
        float leftEdge = screenPos.x - pivotX * tooltipW;
        float rightEdge = leftEdge + tooltipW;
        if (leftEdge < 0f)
            screenPos.x -= leftEdge;
        else if (rightEdge > Screen.width)
            screenPos.x -= (rightEdge - Screen.width);

        // Overlay canvas: pass null as camera for RectTransformUtility
        Camera uiCam = _tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _tooltipCanvas.worldCamera;

        RectTransform canvasRect = (RectTransform)_tooltipCanvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, uiCam, out Vector2 localPoint);

        _tooltipRect.localPosition = localPoint;
    }

    protected virtual void OnDisable()
    {
        _instances.Remove(this);
        ReleaseOwnership();
    }
}
