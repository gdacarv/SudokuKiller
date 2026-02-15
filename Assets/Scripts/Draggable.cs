using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Draggable : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    [Header("Rules")]
    public List<DraggableRule> rules = new();

    public DragInputProvider inputProvider;

    private Vector3 _spawnPosition;
    private Vector2Int _originCell;
    private bool _hadOriginCell;
    private int _originalSortingOrder;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private bool _isDragging;
    private Vector3 _dragOffset;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
    }

    void Start()
    {
        _spawnPosition = transform.position;
    }

    void Update()
    {
        if (inputProvider == null) return;

        if (inputProvider.IsPressed)
        {
            Vector3 pointerPos = inputProvider.PointerWorldPosition;
            if (_collider.OverlapPoint(pointerPos))
                BeginDrag(pointerPos);
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
    }

    private void UpdateDragTint(Vector2Int? cell)
    {
        if (_spriteRenderer == null) return;

        bool isBlocked = false;
        if (cell.HasValue && gridManager != null)
        {
            bool rulesPass = true;
            foreach (var rule in rules)
                if (!rule.CanPlace(gridManager, this, cell.Value.y, cell.Value.x))
                { rulesPass = false; break; }

            isBlocked = !rulesPass || !gridManager.IsCellAvailable(cell.Value.y, cell.Value.x);
        }

        _spriteRenderer.color = isBlocked
            ? new Color(1f, 0.3f, 0.3f, 0.6f)
            : new Color(0.8f, 0.8f, 0.8f, 0.6f);
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
            }
            else
            {
                _hadOriginCell = false;
            }
        }

        if (_spriteRenderer != null)
        {
            _originalSortingOrder = _spriteRenderer.sortingOrder;
            _spriteRenderer.sortingOrder = 100;
        }
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
        if (cell.HasValue)
        {
            foreach (var rule in rules)
                if (!rule.CanPlace(gridManager, this, cell.Value.y, cell.Value.x))
                { rulesPass = false; break; }
        }

        if (cell.HasValue && rulesPass && gridManager.TryPlace(this, cell.Value.y, cell.Value.x))
        {
            transform.position = gridManager.GetCellCenter(cell.Value.y, cell.Value.x);
        }
        else if (!cell.HasValue)
        {
            // Dropped outside the grid — return to spawn position
            transform.position = _spawnPosition;
            if (_hadOriginCell)
                gridManager.TryPlace(this, _originCell.y, _originCell.x);
        }
        else
        {
            // Dropped inside the grid but on a blocked/occupied cell — return to previous cell
            if (_hadOriginCell)
            {
                gridManager.TryPlace(this, _originCell.y, _originCell.x);
                transform.position = gridManager.GetCellCenter(_originCell.y, _originCell.x);
            }
            else
            {
                transform.position = _spawnPosition;
            }
        }
    }
}
