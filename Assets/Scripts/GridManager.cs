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
        _tagQueryCache?.Clear();
        _occupants = new Draggable[gridOverlay.rows, gridOverlay.cols];
        _blockedByMarker = new bool[gridOverlay.rows, gridOverlay.cols];
        _cellSection = new int[gridOverlay.rows, gridOverlay.cols];
        _sectionCells = null;
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
        if (entity != null && _entities.Add(entity)) _tagQueryCache?.Clear();
    }

    public void UnregisterEntity(GridEntity entity)
    {
        if (_entities.Remove(entity)) _tagQueryCache?.Clear();
    }

    // Tag-match results memoized per pattern-list *reference* (rule assets hold stable lists).
    // Only active between BeginTagQueryCache/EndTagQueryCache, so gameplay and editing never see
    // stale results. Caches tag matching only — IsOnGrid changes during a search and is applied per call.
    private Dictionary<List<GridEntity.TagEntry>, List<GridEntity>> _tagQueryCache;

    /// <summary>
    /// Start memoizing tag queries. Only valid while entity tags and registrations are fixed
    /// (e.g. a verifier search that moves draggables but never edits tags). Must be paired with EndTagQueryCache.
    /// </summary>
    public void BeginTagQueryCache()
    {
        _tagQueryCache = new Dictionary<List<GridEntity.TagEntry>, List<GridEntity>>();
        GridEntity.SetParentCacheEnabled(true);
    }

    public void EndTagQueryCache()
    {
        _tagQueryCache = null;
        GridEntity.SetParentCacheEnabled(false);
    }

    /// <summary>
    /// Registered entities matching 'pattern', regardless of IsOnGrid. While the tag query cache is
    /// active the returned list is shared — callers MUST NOT mutate it and must check IsOnGrid themselves.
    /// </summary>
    public List<GridEntity> GetTaggedEntities(List<GridEntity.TagEntry> pattern)
    {
        if (_tagQueryCache != null && _tagQueryCache.TryGetValue(pattern, out var cached))
            return cached;

        var results = new List<GridEntity>();
        foreach (var entity in _entities)
            if (entity.MatchesAll(pattern))
                results.Add(entity);
        _tagQueryCache?.Add(pattern, results);
        return results;
    }

    public List<GridEntity> FindEntitiesWithTags(List<GridEntity.TagEntry> pattern)
    {
        var results = new List<GridEntity>();
        if (_tagQueryCache != null)
        {
            foreach (var entity in GetTaggedEntities(pattern))
                if (entity.IsOnGrid)
                    results.Add(entity);
            return results;
        }

        foreach (var entity in _entities)
            if (entity.IsOnGrid && entity.MatchesAll(pattern))
                results.Add(entity);
        return results;
    }

    private void ApplyEntityMarkers()
    {
        // Rebuild the overlay's hidden-cell set from scratch every pass. _blockedByMarker
        // is already reallocated in InitializeGridState, but the overlay's hidden set is
        // owned by GridOverlay and only ever appended to, so without this a cell stays
        // carved out of the grid even after its BlockedCellMarker is deleted or moved.
        gridOverlay.ClearHiddenCells();

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
        {
            _cellSection[row, col] = sectionId;
            _sectionCells = null; // section→cells index is rebuilt lazily
        }
    }

    // section id → its cells. Lets per-section counting touch only that section's cells instead of the whole grid.
    private Dictionary<int, List<Vector2Int>> _sectionCells;

    private List<Vector2Int> GetSectionCells(int sectionId)
    {
        if (_sectionCells == null)
        {
            _sectionCells = new Dictionary<int, List<Vector2Int>>();
            for (int r = 0; r < gridOverlay.rows; r++)
                for (int c = 0; c < gridOverlay.cols; c++)
                {
                    int s = _cellSection[r, c];
                    if (s < 0) continue;
                    if (!_sectionCells.TryGetValue(s, out var list))
                        _sectionCells[s] = list = new List<Vector2Int>();
                    list.Add(new Vector2Int(r, c));
                }
        }
        return _sectionCells.TryGetValue(sectionId, out var cells) ? cells : null;
    }

    public int GetSection(int row, int col)
    {
        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
            return -1;
        return _cellSection[row, col];
    }

    public bool CheckBoardRules(Draggable incoming, int row, int col)
    {
        // Empty inspector slots are a common authoring slip — skip them instead of throwing.
        foreach (var rule in boardRules)
            if (rule != null && !rule.CanPlace(this, incoming, row, col))
                return false;
        return true;
    }

    // Temporarily places 'incoming' at (row, col) in BOTH position stores: _occupants and the entity's
    // Row/Col. Occupancy-based rules (NPerRow/Col/Section, UniqueByTagKey, RegionSum) read the former;
    // entity-based rules (DistanceToTag, NeighborCount, LineOfSight, Between...) read the latter and skip
    // anything !IsOnGrid, so simulating only one hides the incoming suspect from half the rules.
    // Dispose restores both. A default instance (out-of-bounds cell) is a no-op.
    private readonly struct SimulatedPlacement : System.IDisposable
    {
        private readonly Draggable[,] _grid;
        private readonly Draggable _incoming;
        private readonly Draggable _previousOccupant;
        private readonly int _row, _col, _previousRow, _previousCol;

        public SimulatedPlacement(Draggable[,] grid, Draggable incoming, int row, int col)
        {
            _grid = grid;
            _incoming = incoming;
            _row = row;
            _col = col;
            _previousOccupant = grid[row, col];
            _previousRow = incoming.Entity.Row;
            _previousCol = incoming.Entity.Col;

            grid[row, col] = incoming;
            incoming.Entity.Row = row;
            incoming.Entity.Col = col;
        }

        public void Dispose()
        {
            if (_incoming == null) return;
            _grid[_row, _col] = _previousOccupant;
            _incoming.Entity.Row = _previousRow;
            _incoming.Entity.Col = _previousCol;
        }
    }

    private SimulatedPlacement SimulatePlacement(Draggable incoming, int row, int col)
    {
        if (row < 0 || row >= gridOverlay.rows || col < 0 || col >= gridOverlay.cols)
            return default;
        return new SimulatedPlacement(_occupants, incoming, row, col);
    }

    // Checks whether placing 'incoming' at (row,col) would violate any existing occupant's own rules or
    // the board rules as seen from that occupant (the reverse of what the incoming suspect's own check covers).
    public bool CheckAllOccupantRules(Draggable incoming, int row, int col)
    {
        using var sim = SimulatePlacement(incoming, row, col);
        for (int r = 0; r < gridOverlay.rows; r++)
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[r, c];
                if (occ == null || occ == incoming) continue;
                foreach (var rule in occ.rules)
                    if (rule != null && !rule.CanPlace(this, occ, r, c))
                        return false;
                if (!CheckBoardRules(occ, r, c))
                    return false;
            }
        return true;
    }

    
    public int CountMatchesInSection(int sectionId, List<GridEntity.TagEntry> pattern, Draggable exclude)
    {
        int count = 0;
        if (sectionId >= 0)
        {
            var cells = GetSectionCells(sectionId);
            if (cells == null) return 0;
            foreach (var cell in cells)
            {
                var occ = _occupants[cell.x, cell.y];
                if (occ != null && occ != exclude && occ.Entity.MatchesAll(pattern))
                    count++;
            }
            return count;
        }

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

    /// <summary>
    /// All occupants sharing 'row'/'col's row, column, or section (per scope), excluding 'exclude'.
    /// Mirrors the exclude-self convention of CountMatchesInRow/Col/Section — callers that need the
    /// dragged entity itself included must fold it in manually using the (row, col) passed to CanPlace.
    /// </summary>
    public List<Draggable> GetOccupantsInRegion(RegionScope scope, int row, int col, Draggable exclude)
    {
        var result = new List<Draggable>();
        if (scope == RegionScope.Board)
        {
            for (int r = 0; r < gridOverlay.rows; r++)
                for (int c = 0; c < gridOverlay.cols; c++)
                {
                    var occ = _occupants[r, c];
                    if (occ != null && occ != exclude)
                        result.Add(occ);
                }
            return result;
        }

        if (scope == RegionScope.Section)
        {
            int section = GetSection(row, col);
            if (section == -1) return result;
            for (int r = 0; r < gridOverlay.rows; r++)
                for (int c = 0; c < gridOverlay.cols; c++)
                {
                    var occ = _occupants[r, c];
                    if (occ != null && occ != exclude && _cellSection[r, c] == section)
                        result.Add(occ);
                }
            return result;
        }

        if (scope == RegionScope.Row)
        {
            for (int c = 0; c < gridOverlay.cols; c++)
            {
                var occ = _occupants[row, c];
                if (occ != null && occ != exclude)
                    result.Add(occ);
            }
            return result;
        }

        // Column
        for (int r = 0; r < gridOverlay.rows; r++)
        {
            var occ = _occupants[r, col];
            if (occ != null && occ != exclude)
                result.Add(occ);
        }
        return result;
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

    
    /// <summary>O(1) release for callers that already know the cell the object occupies.</summary>
    public void Release(Draggable obj, int row, int col)
    {
        if (row >= 0 && row < gridOverlay.rows && col >= 0 && col < gridOverlay.cols && _occupants[row, col] == obj)
            _occupants[row, col] = null;
    }

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
            if (rule != null && !rule.CanPlace(this, suspect, suspect.Entity.Row, suspect.Entity.Col))
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
        using var sim = targetCell.HasValue
            ? SimulatePlacement(incoming, targetCell.Value.y, targetCell.Value.x)
            : default;

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
    }

}
