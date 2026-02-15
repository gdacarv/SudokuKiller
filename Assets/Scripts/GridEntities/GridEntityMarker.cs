using UnityEngine;

[ExecuteAlways]
public abstract class GridEntityMarker : MonoBehaviour
{
    public GridManager gridManager;

    public bool hideAtRuntime = true;

    [HideInInspector] public int row;
    [HideInInspector] public int col;


    protected virtual void Awake()
    {
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

        transform.position = gridManager.GetCellCenter(row, col);
    }

    public abstract void ApplyRule(GridManager manager);
}
