using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridOverlay gridOverlay;

    [Header("Blocked Cells (flat row-major array, length rows x cols)")]
    public bool[] blockedCells = new bool[0];

    private Draggable[,] _occupants;
    private bool[,] _blockedByMarker;

void Awake()
    {
        if (gridOverlay == null)
            gridOverlay = GetComponent<GridOverlay>();

        if (gridOverlay == null)
        {
            Debug.LogError("[GridManager] No GridOverlay found! Assign it in the Inspector.", this);
            return;
        }

        _occupants = new Draggable[gridOverlay.rows, gridOverlay.cols];
        _blockedByMarker = new bool[gridOverlay.rows, gridOverlay.cols];
        ApplyEntityMarkers();
        Debug.Log($"[GridManager] Awake: rows={gridOverlay.rows}, cols={gridOverlay.cols}");
    }

public bool IsCellAvailable(int row, int col)
    {
        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
            return false;

        int flatIndex = row * gridOverlay.cols + col;
        if (flatIndex < blockedCells.Length && blockedCells[flatIndex])
            return false;

        if (_blockedByMarker != null && _blockedByMarker[row, col])
            return false;

        return _occupants[row, col] == null;
    }

    public Vector3 GetCellCenter(int row, int col)
    {
        Vector3 origin = transform.position + new Vector3(gridOverlay.offset.x, gridOverlay.offset.y, 0f);
        float totalW = gridOverlay.cellWidth  * gridOverlay.cols;
        float totalH = gridOverlay.cellHeight * gridOverlay.rows;
        float left   = origin.x - totalW * 0.5f;
        float bottom = origin.y - totalH * 0.5f;

        float x = left   + (col + 0.5f) * gridOverlay.cellWidth;
        float y = bottom + (row + 0.5f) * gridOverlay.cellHeight;
        return new Vector3(x, y, 0f);
    }

    public Vector2Int? WorldToCell(Vector3 worldPos)
    {
        Vector3 origin = transform.position + new Vector3(gridOverlay.offset.x, gridOverlay.offset.y, 0f);
        float totalW = gridOverlay.cellWidth  * gridOverlay.cols;
        float totalH = gridOverlay.cellHeight * gridOverlay.rows;
        float left   = origin.x - totalW * 0.5f;
        float bottom = origin.y - totalH * 0.5f;

        float localX = worldPos.x - left;
        float localY = worldPos.y - bottom;

        int col = Mathf.FloorToInt(localX / gridOverlay.cellWidth);
        int row = Mathf.FloorToInt(localY / gridOverlay.cellHeight);

        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
            return null;

        return new Vector2Int(col, row);
    }

private void ApplyEntityMarkers()
    {
        var markers = GetComponentsInChildren<GridEntityMarker>(includeInactive: true);
        Debug.Log($"[GridManager] Found {markers.Length} entity marker(s).");
        foreach (var marker in markers)
        {
            var cell = WorldToCell(marker.transform.position);
            if (cell == null)
            {
                Debug.LogWarning($"[GridManager] Marker '{marker.name}' is outside the grid bounds — skipped.", marker);
                continue;
            }
            marker.row = cell.Value.y;
            marker.col = cell.Value.x;
            Debug.Log($"[GridManager] Applying marker '{marker.name}' row={marker.row} col={marker.col}");
            marker.ApplyRule(this);
        }
        gridOverlay.RefreshGrid();
    }

    public void MarkBlocked(int row, int col)
    {
        if (row >= 0 && row < gridOverlay.rows && col >= 0 && col < gridOverlay.cols)
            _blockedByMarker[row, col] = true;
    }

public void HideGridCell(int row, int col)
    {
        gridOverlay.HideCell(row, col);
    }


    
    public bool TryPlace(Draggable obj, int row, int col)
    {
        if (!IsCellAvailable(row, col))
            return false;

        _occupants[row, col] = obj;
        return true;
    }

    public void Release(Draggable obj)
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                if (_occupants[r, c] == obj)
                    _occupants[r, c] = null;
    }
}
