using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HoverTooltip : MonoBehaviour
{
    [Header("Tooltip Content")]
    public List<string> lines = new();

    [Header("Behaviour")]
    public float hoverDelay = 0.5f;
    public Vector2 tooltipOffset = new(0f, 60f);

    [Header("References")]
    public DragInputProvider inputProvider;
    public TooltipUI tooltipUI;

    private Collider2D _collider;
    private float _hoverTimer;
    private bool _isShowing;
    private Canvas _tooltipCanvas;
    private RectTransform _tooltipRect;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
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
        bool isHovering = _collider.OverlapPoint(pointerWorldPos);
        bool isHeld = inputProvider.IsHeld;

        if (isHovering && !isHeld)
        {
            _hoverTimer += Time.deltaTime;

            if (_hoverTimer >= hoverDelay && !_isShowing)
                ShowTooltip();

            if (_isShowing)
                PositionTooltip();
        }
        else
        {
            if (_isShowing)
                HideTooltip();
            _hoverTimer = 0f;
        }
    }

    private void ShowTooltip()
    {
        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetLines(lines);
        PositionTooltip();
    }

    private void HideTooltip()
    {
        _isShowing = false;
        tooltipUI.gameObject.SetActive(false);
    }

    private void PositionTooltip()
    {
        if (_tooltipCanvas == null || _tooltipRect == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        screenPos.x += tooltipOffset.x;
        screenPos.y += tooltipOffset.y;

        // Overlay canvas: pass null as camera for RectTransformUtility
        Camera uiCam = _tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _tooltipCanvas.worldCamera;

        RectTransform canvasRect = (RectTransform)_tooltipCanvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, uiCam, out Vector2 localPoint);

        _tooltipRect.localPosition = localPoint;
    }

    void OnDisable()
    {
        if (_isShowing)
            HideTooltip();
    }
}
