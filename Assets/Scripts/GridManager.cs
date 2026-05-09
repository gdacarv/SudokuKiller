using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridOverlay gridOverlay;

    [Header("Board Rules")]
    public List<Rule> boardRules = new();

    [Header("Killer Rules")]
    public List<Rule> killerRules = new();

    [Header("Validation Settings")]
    public bool preventInvalidPlacement = true;
    public bool highlightRuleViolations = false;
    public bool startAtSolutionPositions = false;

    [Header("Display")]
    public bool showGridOverlay = true;

    private Draggable[,] _occupants;
#if UNITY_EDITOR
    [System.NonSerialized] private bool _prevHighlightRuleViolations;
#endif
    private bool[,] _blockedByMarker;
    private int[,] _cellSection;
    private readonly HashSet<GridEntity> _entities = new();

    void Awake()
    {
        if (gridOverlay == null)
            gridOverlay = GetComponent<GridOverlay>();

        if (gridOverlay == null)
        {
            Debug.LogError("[GridManager] No GridOverlay found! Assign it in the Inspector.", this);
            return;
        }

        InitializeGridState();
        gridOverlay.SetVisible(showGridOverlay);
        Debug.Log($"[GridManager] Awake: rows={gridOverlay.rows}, cols={gridOverlay.cols}");
    }

    private void InitializeGridState()
    {
        _entities.Clear();
        _occupants = new Draggable[gridOverlay.rows, gridOverlay.cols];
        _blockedByMarker = new bool[gridOverlay.rows, gridOverlay.cols];
        _cellSection = new int[gridOverlay.rows, gridOverlay.cols];
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                _cellSection[r, c] = -1;
        ApplyEntityMarkers();
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

    public void RegisterEntity(GridEntity entity)
    {
        if (entity != null) _entities.Add(entity);
    }

    public void UnregisterEntity(GridEntity entity)
    {
        _entities.Remove(entity);
    }

    public List<GridEntity> FindEntitiesWithTags(List<GridEntity.TagEntry> pattern)
    {
        var results = new List<GridEntity>();
        foreach (var entity in _entities)
            if (entity.IsOnGrid && entity.MatchesAll(pattern))
                results.Add(entity);
        return results;
    }

    private void ApplyEntityMarkers()
    {
        var markers = Object.FindObjectsByType<GridEntityMarker>(FindObjectsInactive.Exclude);
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
            //Debug.Log($"[GridManager] Applying marker '{marker.name}' row={marker.row} col={marker.col}");
            marker.ApplyRule(this);

            var entity = marker.GetComponent<GridEntity>();
            if (entity != null)
            {
                entity.Row = marker.row;
                entity.Col = marker.col;
                RegisterEntity(entity);
            }
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

    
    public int CountMatchesInSection(int sectionId, List<GridEntity.TagEntry> pattern, Draggable exclude)
    {
        int count = 0;
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (_cellSection[r, c] == sectionId && occ != null && occ != exclude && occ.Entity.MatchesAll(pattern))
                    count++;
            }
        return count;
    }

    public bool HasMatchInSection(int sectionId, List<GridEntity.TagEntry> pattern, Draggable exclude)
        => CountMatchesInSection(sectionId, pattern, exclude) > 0;

    
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

    public int CountMatchesInRow(int row, List<GridEntity.TagEntry> pattern, Draggable exclude)
    {
        int count = 0;
        for (int c = 0; c < gridOverlay.cols; c++)
        {
            var occ = _occupants[row, c];
            if (occ != null && occ != exclude && occ.Entity.MatchesAll(pattern))
                count++;
        }
        return count;
    }

    public bool HasMatchInRow(int row, List<GridEntity.TagEntry> pattern, Draggable exclude)
        => CountMatchesInRow(row, pattern, exclude) > 0;

    public int CountMatchesInCol(int col, List<GridEntity.TagEntry> pattern, Draggable exclude)
    {
        int count = 0;
        for (int r = 0; r < gridOverlay.rows; r++)
        {
            var occ = _occupants[r, col];
            if (occ != null && occ != exclude && occ.Entity.MatchesAll(pattern))
                count++;
        }
        return count;
    }

    public bool HasMatchInCol(int col, List<GridEntity.TagEntry> pattern, Draggable exclude)
        => CountMatchesInCol(col, pattern, exclude) > 0;

    
    public void Release(Draggable obj)
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
                if (_occupants[r, c] == obj)
                    _occupants[r, c] = null;
    }


public bool AreAllDraggablesInSolutionCells()
    {
        var draggables = Object.FindObjectsByType<Draggable>(FindObjectsSortMode.None);
        foreach (var d in draggables)
        {
            var solution = d.GetComponent<SolutionPosition>();
            if (solution == null) continue;
            if (!solution.IsInSolutionCell())
                return false;
        }
        return true;
    }

    public Rule EvaluateKillerRules(Draggable suspect)
    {
        foreach (var rule in killerRules)
            if (!rule.CanPlace(this, suspect, suspect.Entity.Row, suspect.Entity.Col))
                return rule;
        return null;
    }

    public bool AreAllRulesValid()
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null) continue;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                        return false;
                if (!CheckBoardRules(occ, r, c))
                    return false;
            }
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        EditorApplication.delayCall += () =>
        {
            if (this == null || gridOverlay == null) return;
            gridOverlay.SetVisible(showGridOverlay);
        };

        if (highlightRuleViolations == _prevHighlightRuleViolations) return;
        _prevHighlightRuleViolations = highlightRuleViolations;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            RefreshEditModeViolations();
        };
    }

    public void RefreshEditModeViolations()
    {
        if (gridOverlay == null)
            gridOverlay = GetComponent<GridOverlay>();
        if (gridOverlay == null) return;

        InitializeGridState();

        var solutions = FindObjectsByType<SolutionPosition>(FindObjectsSortMode.None);
        foreach (var sol in solutions)
        {
            var draggable = sol.GetComponent<Draggable>();
            if (draggable == null) continue;

            // Awake doesn't run in edit mode, so Entity may be null — initialize it here
            draggable.EnsureEntityInitialized();

            int r = sol.solutionRow;
            int c = sol.solutionCol;
            bool inBounds = r >= 0 && r < gridOverlay.rows && c >= 0 && c < gridOverlay.cols;

            if (inBounds)
            {
                _occupants[r, c] = draggable;
                draggable.Entity.Row = r;
                draggable.Entity.Col = c;
            }
            else
            {
                draggable.Entity.Row = -1;
                draggable.Entity.Col = -1;
            }
            RegisterEntity(draggable.Entity);
        }

        RefreshViolationHighlights();

        foreach (var sol in solutions)
        {
            if (sol.solutionRow < 0 || sol.solutionCol < 0)
                sol.GetComponent<Draggable>()?.SetHighlight(false);
        }
    }
#endif

    public List<Draggable> GetInvalidDraggables()
    {
        var result = new List<Draggable>();
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null) continue;
                bool valid = true;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { valid = false; break; }
                if (valid)
                    valid = CheckBoardRules(occ, r, c);
                if (!valid)
                    result.Add(occ);
            }
        return result;
    }

    public void RefreshViolationHighlights()
    {
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null) continue;

                if (!highlightRuleViolations)
                {
                    occ.SetHighlight(false);
                    continue;
                }

                bool valid = true;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { valid = false; break; }
                if (valid)
                    valid = CheckBoardRules(occ, r, c);

                occ.SetHighlight(!valid);
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

                bool valid = true;
                foreach (var rule in occ.rules)
                    if (!rule.CanPlace(this, occ, r, c))
                    { valid = false; break; }
                if (valid)
                    valid = CheckBoardRules(occ, r, c);

                occ.SetHighlight(!valid);
            }

        if (simulated)
            _occupants[simRow, simCol] = previous;
    }

}
