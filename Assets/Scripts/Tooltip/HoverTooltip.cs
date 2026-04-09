using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HoverTooltip : MonoBehaviour
{
    [Header("Tooltip Content")]
    public string message = "";

    [Header("Behaviour")]
    public float hoverDelay = 0.5f;
    public Vector2 tooltipOffset = new(0f, 60f);

    [Header("References")]
    public DragInputProvider inputProvider;
    public TooltipUI tooltipUI;

    protected Collider2D _collider;
    private float _hoverTimer;
    protected bool _isShowing;
    private Canvas _tooltipCanvas;
    private RectTransform _tooltipRect;

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

    protected virtual void ShowTooltip()
    {
        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetText(message);
        PositionTooltip();
    }

    private void HideTooltip()
    {
        _isShowing = false;
        tooltipUI.gameObject.SetActive(false);
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

    void OnDisable()
    {
        if (_isShowing)
            HideTooltip();
    }
}
