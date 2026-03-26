using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CursorManager : MonoBehaviour
{
    private enum CursorState { Default, DragStart, Dragging, DragEnd, IdentifyMode, Identified }

    [Header("Cursor Sprites")]
    [SerializeField] private Sprite defaultCursor;
    [SerializeField] private Sprite clickCursor;
    [SerializeField] private Sprite dragCursor;
    [SerializeField] private Sprite[] identifyFrames;
    [SerializeField] private Sprite identifiedCursor;

    [Header("Timing")]
    [SerializeField] private float clickFlashDuration = 0.15f;
    [SerializeField] private float identifyAnimFPS = 6f;

    [Header("References")]
    [SerializeField] private Image cursorImage;
    [SerializeField] private IdentifyKillerButton identifyKillerButton;
    [SerializeField] private GameObject identifyTooltipPanel;
    [SerializeField] private TextMeshProUGUI identifyTooltipText;

    [Header("Tooltip")]
    [SerializeField] private string identifyTooltipString = "Who is the killer?";

    private RectTransform _cursorRect;
    private Vector2 _defaultPivot;
    private Draggable[] _draggables;

    private CursorState _state = CursorState.Default;
    private bool _wasDragging;
    private float _flashTimer;
    private float _identifyAnimTime;

    void Awake()
    {
        _cursorRect = cursorImage.GetComponent<RectTransform>();
        _defaultPivot = _cursorRect.pivot;
    }

    void Start()
    {
        _draggables = FindObjectsByType<Draggable>(FindObjectsSortMode.None);
        if (identifyTooltipText != null)
            identifyTooltipText.text = identifyTooltipString;
        if (identifyTooltipPanel != null)
            identifyTooltipPanel.SetActive(false);
    }

    void OnEnable()
    {
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        UpdateState();
        ApplySprite();    // pivot must be set before position
        PositionCursor();
    }

    private void PositionCursor()
    {
        _cursorRect.anchoredPosition = Mouse.current.position.ReadValue();
    }

    private void UpdateState()
    {
        // Terminal state: once identified, never leave
        if (_state == CursorState.Identified) return;

        bool anyDragging = false;
        foreach (var d in _draggables)
            if (d != null && d.IsDragging) { anyDragging = true; break; }

        bool dragStarted = !_wasDragging && anyDragging;
        bool dragEnded = _wasDragging && !anyDragging;
        _wasDragging = anyDragging;

        // Identified check (highest priority)
        if (identifyKillerButton != null && identifyKillerButton.WasClicked)
        {
            _state = CursorState.Identified;
            return;
        }

        // Handle flash timer states
        if (_state == CursorState.DragStart || _state == CursorState.DragEnd)
        {
            if (_state == CursorState.DragEnd && dragStarted)
            {
                _state = CursorState.DragStart;
                _flashTimer = clickFlashDuration;
                return;
            }

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                _state = (_state == CursorState.DragStart) ? CursorState.Dragging : CursorState.Default;
            return;
        }

        // Drag transitions
        if (dragStarted)
        {
            _state = CursorState.DragStart;
            _flashTimer = clickFlashDuration;
            return;
        }
        if (dragEnded && _state == CursorState.Dragging)
        {
            _state = CursorState.DragEnd;
            _flashTimer = clickFlashDuration;
            return;
        }
        if (anyDragging)
        {
            _state = CursorState.Dragging;
            return;
        }

        // Identify mode — driven by button toggle
        if (identifyKillerButton != null && identifyKillerButton.IsInIdentifyMode)
        {
            _state = CursorState.IdentifyMode;
            return;
        }

        _state = CursorState.Default;
    }

    private void ApplySprite()
    {
        Sprite sprite = defaultCursor;

        switch (_state)
        {
            case CursorState.Default:    sprite = defaultCursor;       break;
            case CursorState.DragStart:
            case CursorState.DragEnd:    sprite = clickCursor;         break;
            case CursorState.Dragging:   sprite = dragCursor;          break;
            case CursorState.IdentifyMode: sprite = GetIdentifyFrame(); break;
            case CursorState.Identified: sprite = identifiedCursor;    break;
        }

        if (cursorImage.sprite != sprite)
            cursorImage.sprite = sprite;

        // Identify/Identified sprites are centered; all others use the saved pivot
        bool centered = _state == CursorState.IdentifyMode || _state == CursorState.Identified;
        _cursorRect.pivot = centered ? new Vector2(0.5f, 0.5f) : _defaultPivot;

        if (identifyTooltipPanel != null)
            identifyTooltipPanel.SetActive(_state == CursorState.IdentifyMode);
    }

    private Sprite GetIdentifyFrame()
    {
        if (identifyFrames == null || identifyFrames.Length == 0) return defaultCursor;
        _identifyAnimTime += Time.deltaTime;
        float pingPong = Mathf.PingPong(_identifyAnimTime * identifyAnimFPS, identifyFrames.Length - 1);
        return identifyFrames[Mathf.RoundToInt(pingPong)];
    }
}
