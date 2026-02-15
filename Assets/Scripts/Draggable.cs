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
            {
                transform.position = inputProvider.PointerWorldPosition + _dragOffset;
            }
            else
            {
                EndDrag();
            }
        }
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
            _spriteRenderer.sortingOrder = _originalSortingOrder;

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
        else
        {
            transform.position = _spawnPosition;
            if (_hadOriginCell)
                gridManager.TryPlace(this, _originCell.y, _originCell.x);
        }
    }
}
