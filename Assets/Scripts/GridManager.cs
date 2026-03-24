using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridOverlay gridOverlay;

    [Header("Board Rules")]
    public List<BoardRule> boardRules = new();

    [Header("Validation Settings")]
    public bool preventInvalidPlacement = true;
    public bool highlightRuleViolations = false;

    private Draggable[,] _occupants;
    private bool[,] _blockedByMarker;
    private int[,] _cellSection;

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
        _cellSection = new int[gridOverlay.rows, gridOverlay.cols];
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                _cellSection[r, c] = -1;
        ApplyEntityMarkers();
        Debug.Log($"[GridManager] Awake: rows={gridOverlay.rows}, cols={gridOverlay.cols}");
    }

    public bool IsCellAvailable(int row, int col)
    {
        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
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

    public void RegisterSection(int row, int col, int sectionId)
    {
        if (row >= 0 && row < gridOverlay.rows && col >= 0 && col < gridOverlay.cols)
            _cellSection[row, col] = sectionId;
    }

    public int GetSection(int row, int col)
    {
        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
            return -1;
        return _cellSection[row, col];
    }

    public bool CheckBoardRules(Draggable incoming, int row, int col)
    {
        foreach (var rule in boardRules)
            if (!rule.CanPlace(this, incoming, row, col))
                return false;
        return true;
    }

    // Checks whether placing 'incoming' at (row,col) would violate any existing occupant's rules.
    public bool CheckAllOccupantRules(Draggable incoming, int row, int col)
    {
        var previous = _occupants[row, col];
        _occupants[row, col] = incoming;
        bool ok = true;
        for (int r = 0; r < gridOverlay.rows && ok; r++)
            for (int c = 0; c < gridOverlay.cols && ok; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null || occ == incoming) continue;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { ok = false; break; }
            }
        _occupants[row, col] = previous;
        return ok;
    }

    
    public bool HasTagInSection(int sectionId, string tag, Draggable exclude)
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                if (_cellSection[r, c] == sectionId &&
                    _occupants[r, c] != null &&
                    _occupants[r, c] != exclude &&
                    _occupants[r, c].CompareTag(tag))
                    return true;
        return false;
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

    public bool HasTagInRow(int row, string tag, Draggable exclude)
    {
        for (int c = 0; c < gridOverlay.cols; c++)
            if (_occupants[row, c] != null && _occupants[row, c] != exclude && _occupants[row, c].CompareTag(tag))
                return true;
        return false;
    }

    public bool HasTagInCol(int col, string tag, Draggable exclude)
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            if (_occupants[r, col] != null && _occupants[r, col] != exclude && _occupants[r, col].CompareTag(tag))
                return true;
        return false;
    }

    
    public void Release(Draggable obj)
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                if (_occupants[r, c] == obj)
                    _occupants[r, c] = null;
    }


public void RefreshViolationHighlights()
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null) continue;

                var sr = occ.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                if (!highlightRuleViolations)
                {
                    sr.color = Color.white;
                    continue;
                }

                bool valid = true;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { valid = false; break; }
                if (valid)
                    valid = CheckBoardRules(occ, r, c);

                sr.color = valid ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
            }
    }

public void UpdateDragHighlights(Draggable incoming, Vector2Int? targetCell)
    {
        if (!highlightRuleViolations)
        {
            RefreshViolationHighlights();
            return;
        }

        // Simulate incoming at targetCell so occupant rule checks see it as a neighbour
        bool simulated = false;
        int simRow = -1, simCol = -1;
        Draggable previous = null;
        if (targetCell.HasValue)
        {
            simRow = targetCell.Value.y;
            simCol = targetCell.Value.x;
            if (simRow >= 0 && simRow < gridOverlay.rows && simCol >= 0 && simCol < gridOverlay.cols)
            {
                previous = _occupants[simRow, simCol];
                _occupants[simRow, simCol] = incoming;
                simulated = true;
            }
        }

        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null || occ == incoming) continue;

                var sr = occ.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                bool valid = true;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { valid = false; break; }
                if (valid)
                    valid = CheckBoardRules(occ, r, c);

                sr.color = valid ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
            }

        if (simulated)
            _occupants[simRow, simCol] = previous;
    }

}
