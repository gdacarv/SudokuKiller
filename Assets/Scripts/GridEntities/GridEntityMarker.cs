using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(GridEntity))]
public abstract class GridEntityMarker : MonoBehaviour
{
    public GridManager gridManager;

    public bool hideAtRuntime = true;

    [HideInInspector] public int row;
    [HideInInspector] public int col;

    private GridEntity _entity;

    protected virtual void Awake()
    {
        _entity = GetComponent<GridEntity>();

        if (Application.isPlaying && hideAtRuntime)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }

    protected virtual void Update()
    {
        if (Application.isPlaying) return;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager == null) return;

        var cell = gridManager.WorldToCell(transform.position);
        if (cell == null) return;

        col = cell.Value.x;
        row = cell.Value.y;

        if (_entity == null) _entity = GetComponent<GridEntity>();
        if (_entity != null)
        {
            _entity.Row = row;
            _entity.Col = col;
        }

        transform.position = gridManager.GetCellCenter(row, col);
    }

    public abstract void ApplyRule(GridManager manager);
}
