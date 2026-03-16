using System.Collections;
using UnityEngine;

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
    private bool _wasClicked;
    private float _checkTimer;
    private const float CheckInterval = 0.25f;

    private const string InactiveTooltip =
        "You need to place all the suspects, objects and victim\non the map before being able to identify the killer";
    private const string ActiveTooltip = "Click to identify the killer";

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _defaultSprite = _spriteRenderer.sprite;
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
                SetInactive();
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
        if (hoverTooltip != null)
            hoverTooltip.message = ActiveTooltip;
        StartCoroutine(PulseCoroutine());
    }

    private void SetInactive()
    {
        _isActive = false;
        StopAllCoroutines();
        _spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        _spriteRenderer.transform.localScale = Vector3.one;
        if (hoverTooltip != null)
            hoverTooltip.message = InactiveTooltip;
    }

    private void OnClicked()
    {
        _wasClicked = true;
        StopAllCoroutines();
        _spriteRenderer.transform.localScale = Vector3.one;
        _spriteRenderer.color = Color.white;
        if (pressedSprite != null)
            _spriteRenderer.sprite = pressedSprite;
        Debug.Log("[IdentifyKillerButton] Killer identified!");
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
