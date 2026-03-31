using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(GridEntity))]
public class Draggable : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    [Header("Rules")]
    public List<DraggableRule> rules = new();

    public DragInputProvider inputProvider;

    public GridEntity Entity { get; private set; }

    private Vector3 _spawnPosition;
    private Vector2Int _originCell;
    private bool _hadOriginCell;
    private int _originalSortingOrder;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private bool _isDragging;
    public bool IsDragging => _isDragging;
    private Vector3 _dragOffset;
    private IdentifyKillerButton _identifyKillerButton;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        Entity = GetComponent<GridEntity>();
    }

    void Start()
    {
        _spawnPosition = transform.position;

        var solution = GetComponent<SolutionPosition>();
        if (solution != null)
        {
            _spawnPosition = solution.uiPosition;
            transform.position = _spawnPosition;
        }

        gridManager?.RegisterEntity(Entity);
        _identifyKillerButton = FindFirstObjectByType<IdentifyKillerButton>();
    }

    void OnDestroy()
    {
        gridManager?.UnregisterEntity(Entity);
    }

    void Update()
    {
        if (inputProvider == null) return;

        if (inputProvider.IsPressed)
        {
            Vector3 pointerPos = inputProvider.PointerWorldPosition;
            if (_collider.OverlapPoint(pointerPos))
            {
                if (_identifyKillerButton != null && _identifyKillerButton.IsInIdentifyMode)
                    _identifyKillerButton.OnSuspectClicked(this);
                else
                    BeginDrag(pointerPos);
            }
        }

        if (_isDragging)
        {
            if (inputProvider.IsHeld)
                UpdateDrag();
            else
                EndDrag();
        }
    }

private void UpdateDrag()
    {
        Vector3 pointerPos = inputProvider.PointerWorldPosition;
        Vector2Int? cell = gridManager != null ? gridManager.WorldToCell(pointerPos) : null;

        if (cell.HasValue && gridManager != null)
            transform.position = gridManager.GetCellCenter(cell.Value.y, cell.Value.x);
        else
            transform.position = pointerPos + _dragOffset;

        UpdateDragTint(cell);
        gridManager?.UpdateDragHighlights(this, cell);
    }

private void UpdateDragTint(Vector2Int? cell)
    {
        if (_spriteRenderer == null) return;

        bool isBlocked = false;
        bool isViolating = false;
        if (cell.HasValue && gridManager != null)
        {
            isBlocked = !gridManager.IsCellAvailable(cell.Value.y, cell.Value.x);

            if (!isBlocked && (gridManager.preventInvalidPlacement || gridManager.highlightRuleViolations))
            {
                bool rulesPass = true;
                foreach (var rule in rules)
                    if (!rule.CanPlace(gridManager, this, cell.Value.y, cell.Value.x))
                    { rulesPass = false; break; }
                if (rulesPass)
                    rulesPass = gridManager.CheckBoardRules(this, cell.Value.y, cell.Value.x);

                isViolating = !rulesPass;
            }
        }

        if (isBlocked)
            _spriteRenderer.color = new Color(1f, 0.3f, 0.3f, 0.6f);   // red — cell unavailable
        else if (isViolating)
            _spriteRenderer.color = new Color(1f, 0.6f, 0.1f, 0.6f);   // orange — rule violation
        else
            _spriteRenderer.color = new Color(0.8f, 0.8f, 0.8f, 0.6f); // normal drag tint
    }

private void BeginDrag(Vector3 pointerPos)
    {
        _isDragging = true;
        _dragOffset = transform.position - pointerPos;

        if (gridManager != null)
        {
            Vector2Int? cell = gridManager.WorldToCell(transform.position);
            if (cell.HasValue)
            {
                _originCell = cell.Value;
                _hadOriginCell = true;
                gridManager.Release(this);
                gridManager.RefreshViolationHighlights();
            }
            else
            {
                _hadOriginCell = false;
            }
        }

        Entity.Row = -1;
        Entity.Col = -1;

        if (_spriteRenderer != null)
        {
            _originalSortingOrder = _spriteRenderer.sortingOrder;
            _spriteRenderer.sortingOrder = 100;
        }
    }

public void Flash()
    {
        StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        for (int i = 0; i < 3; i++)
        {
            _spriteRenderer.color = new Color(1f, 0.3f, 0.3f, 1f);
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
        }
        gridManager?.RefreshViolationHighlights();
    }

    private void EndDrag()
    {
        _isDragging = false;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingOrder = _originalSortingOrder;
            _spriteRenderer.color = Color.white;
        }

        if (gridManager == null) return;

        Vector2Int? cell = gridManager.WorldToCell(transform.position);

        bool rulesPass = true;
        if (cell.HasValue && gridManager.preventInvalidPlacement)
        {
            foreach (var rule in rules)
                if (!rule.CanPlace(gridManager, this, cell.Value.y, cell.Value.x))
                { rulesPass = false; break; }
            if (rulesPass)
                rulesPass = gridManager.CheckAllOccupantRules(this, cell.Value.y, cell.Value.x);
            if (rulesPass)
                rulesPass = gridManager.CheckBoardRules(this, cell.Value.y, cell.Value.x);
        }

        if (cell.HasValue && rulesPass && gridManager.TryPlace(this, cell.Value.y, cell.Value.x))
        {
            Entity.Row = cell.Value.y;
            Entity.Col = cell.Value.x;
            transform.position = gridManager.GetCellCenter(cell.Value.y, cell.Value.x);
        }
        else if (!cell.HasValue)
        {
            // Dropped outside the grid — return to spawn position
            transform.position = _spawnPosition;
        }
        else
        {
            // Dropped inside the grid but on a blocked/occupied/invalid cell — return to previous cell
            if (_hadOriginCell)
            {
                gridManager.TryPlace(this, _originCell.y, _originCell.x);
                Entity.Row = _originCell.y;
                Entity.Col = _originCell.x;
                transform.position = gridManager.GetCellCenter(_originCell.y, _originCell.x);
            }
            else
            {
                transform.position = _spawnPosition;
            }
        }

        gridManager.RefreshViolationHighlights();
    }
}
