using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class IdentifyKillerButton : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public DragInputProvider inputProvider;
    public HoverTooltip hoverTooltip;

    [Header("Sprites")]
    public Sprite pressedSprite;

    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private Sprite _defaultSprite;

    private bool _isActive;
    private bool _isInIdentifyMode;
    private bool _wasClicked;
    public bool IsActive => _isActive;
    public bool IsInIdentifyMode => _isInIdentifyMode;
    public bool WasClicked => _wasClicked;

    [Header("Localization")]
    [SerializeField] private LocalizedString inactiveTooltipLocalized;
    [SerializeField] private LocalizedString activeTooltipLocalized;
    [SerializeField] private LocalizedString popupAllSuspectsLocalized;
    [SerializeField] private LocalizedString popupCluesNotRespectedLocalized;

    private float _checkTimer;
    private const float CheckInterval = 0.25f;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _defaultSprite = _spriteRenderer.sprite;
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(Locale _)
    {
        if (hoverTooltip == null) return;
        hoverTooltip.message = _isActive
            ? activeTooltipLocalized.GetLocalizedString()
            : inactiveTooltipLocalized.GetLocalizedString();
    }

    void Start()
    {
        SetInactive();
    }

    void Update()
    {
        if (_wasClicked) return;

        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = CheckInterval;
            bool allPlaced = AreAllDraggablesPlaced();
            if (allPlaced && !_isActive)
                SetActive();
            else if (!allPlaced && _isActive)
            {
                if (_isInIdentifyMode) ExitIdentifyMode();
                SetInactive();
            }
        }

        if (_isActive && inputProvider != null && inputProvider.IsPressed)
        {
            if (_collider.OverlapPoint(inputProvider.PointerWorldPosition))
                OnClicked();
        }
    }

    private bool AreAllDraggablesPlaced()
    {
        if (gridManager == null) return false;

        var draggables = FindObjectsByType<Draggable>(FindObjectsSortMode.None);
        if (draggables.Length == 0) return false;

        foreach (var d in draggables)
        {
            if (d.IsDragging) return false;
            Vector2Int? cell = gridManager.WorldToCell(d.transform.position);
            if (!cell.HasValue) return false;
        }
        return true;
    }

    private void SetActive()
    {
        _isActive = true;
        _spriteRenderer.color = Color.white;
        _spriteRenderer.sprite = _defaultSprite;
        if (hoverTooltip != null)
            hoverTooltip.message = activeTooltipLocalized.GetLocalizedString();
        StartCoroutine(PulseCoroutine());
    }

    private void SetInactive()
    {
        _isActive = false;
        StopAllCoroutines();
        _spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        _spriteRenderer.sprite = _defaultSprite;
        _spriteRenderer.transform.localScale = Vector3.one;
        if (hoverTooltip != null)
            hoverTooltip.message = inactiveTooltipLocalized.GetLocalizedString();
    }

    private void OnClicked()
    {
        if (_isInIdentifyMode)
        {
            ExitIdentifyMode();
            return;
        }

        // Check 1: all draggables must be placed on the grid
        var draggables = FindObjectsByType<Draggable>(FindObjectsSortMode.None);
        bool anyUnplaced = false;
        foreach (var d in draggables)
        {
            if (d.IsDragging || !gridManager.WorldToCell(d.transform.position).HasValue)
            {
                d.Flash();
                anyUnplaced = true;
            }
        }
        if (anyUnplaced)
        {
            PopupMessage.Show(popupAllSuspectsLocalized.GetLocalizedString());
            return;
        }

        // Check 2: all rules must be valid
        if (!gridManager.AreAllRulesValid())
        {
            PopupMessage.Show(popupCluesNotRespectedLocalized.GetLocalizedString());
            foreach (var d in gridManager.GetInvalidDraggables())
                d.Flash();
            return;
        }

        EnterIdentifyMode();
    }

    private void EnterIdentifyMode()
    {
        _isInIdentifyMode = true;
        StopAllCoroutines();
        _spriteRenderer.transform.localScale = Vector3.one;
        _spriteRenderer.color = Color.white;
        if (pressedSprite != null)
            _spriteRenderer.sprite = pressedSprite;
    }

    private void ExitIdentifyMode()
    {
        _isInIdentifyMode = false;
        SetActive();
    }

    private IEnumerator PulseCoroutine()
    {
        while (true)
        {
            float t = Mathf.PingPong(Time.time * 2f, 1f);
            float scale = Mathf.Lerp(1f, 1.1f, t);
            _spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }
}
