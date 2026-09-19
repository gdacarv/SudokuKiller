using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Capture / apply core shared by BoardLayoutsWindow and LevelRulesWindow, so the
// "move every draggable to these cells" step exists exactly once.
public static class BoardLayoutIO
{
    public class ApplyReport
    {
        public bool   aborted;
        public string abortReason;
        public int    applied;
        public readonly List<string> missing     = new();  // in the layout, not in the scene
        public readonly List<string> unlisted    = new();  // in the scene, not in the layout
        public readonly List<string> outOfBounds = new();  // saved cell is outside the current grid
        public readonly List<string> collisions  = new();  // applied, but two draggables share a cell

        public bool HasIssues => aborted || missing.Count > 0 || unlisted.Count > 0
                                 || outOfBounds.Count > 0 || collisions.Count > 0;
    }

    public static GridManager FindGrid() => Object.FindFirstObjectByType<GridManager>();

    // The movable set: same definition PuzzleUniquenessVerifier uses. Fails on duplicate
    // names, since layouts address draggables by name and could not tell them apart.
    public static bool TryCollect(out List<Draggable> draggables, out string error)
    {
        draggables = Object.FindObjectsByType<Draggable>(FindObjectsSortMode.None)
            .Where(d => d.GetComponent<SolutionPosition>() != null)
            .OrderBy(d => d.name)
            .ToList();

        var dup = draggables.GroupBy(d => d.name).FirstOrDefault(g => g.Count() > 1);
        error = dup == null
            ? null
            : $"more than one draggable is named '{dup.Key}' — layouts address draggables by name, so give them distinct names";
        return error == null;
    }

    // Reads every draggable's current cell straight from its transform (what
    // SolutionPosition.Update would derive on its next tick), so it is correct even
    // if the editor hasn't ticked since the last drag.
    public static bool TrySnapshot(out List<BoardLayoutEntry> entries, out string error)
    {
        entries = new List<BoardLayoutEntry>();

        var gm = FindGrid();
        if (gm == null || gm.gridOverlay == null)
        {
            error = "no GridManager with a GridOverlay in the scene";
            return false;
        }
        if (!TryCollect(out var draggables, out error)) return false;

        foreach (var d in draggables)
        {
            var pos  = d.transform.position;
            var cell = gm.WorldToCell(pos);   // (x = col, y = row), null when off the board
            entries.Add(new BoardLayoutEntry
            {
                objectName    = d.name,
                row           = cell?.y ?? -1,
                col           = cell?.x ?? -1,
                worldPosition = pos,
            });
        }
        return true;
    }

    // Overwrites the preset's contents with the board as it is now.
    public static bool Capture(BoardLayoutPreset target, out string error)
    {
        if (!TrySnapshot(out var entries, out error)) return false;

        target.sceneName   = SceneManager.GetActiveScene().name;
        target.capturedUtc = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        target.placements  = entries;
        return true;
    }

    // True when both describe the same draggables in the same places.
    public static bool Matches(IReadOnlyList<BoardLayoutEntry> a, IReadOnlyList<BoardLayoutEntry> b)
    {
        if (a.Count != b.Count) return false;

        var byName = new Dictionary<string, BoardLayoutEntry>();
        foreach (var e in b) byName[e.objectName] = e;

        foreach (var p in a)
        {
            if (!byName.TryGetValue(p.objectName, out var l)) return false;
            if (p.IsOnGrid != l.IsOnGrid) return false;
            if (p.IsOnGrid)
            {
                if (p.row != l.row || p.col != l.col) return false;
            }
            else if ((p.worldPosition - l.worldPosition).sqrMagnitude > 1e-6f)
            {
                return false;
            }
        }
        return true;
    }

    // Moves draggables to the entries' cells as one undo step. SolutionPosition re-authors
    // solutionRow/Col from the transform by design, so both are written together — that is
    // also what lets undo restore them as a pair without SolutionPosition.Update fighting it.
    //
    // strict: abort if any entry can't be placed (missing object / cell outside the grid).
    // Otherwise place what matches and report the rest.
    public static ApplyReport Apply(IReadOnlyList<BoardLayoutEntry> entries, string undoLabel,
                                    bool strict, string logTag = "BoardLayouts")
    {
        var report = new ApplyReport();

        var gm = FindGrid();
        if (gm == null || gm.gridOverlay == null)
            return Abort(report, logTag, "no GridManager with a GridOverlay in the scene.");
        if (!TryCollect(out var draggables, out var error))
            return Abort(report, logTag, error + ".");

        var listed = new HashSet<string>();
        foreach (var e in entries)
            if (!listed.Add(e.objectName))
                return Abort(report, logTag, $"the layout lists '{e.objectName}' more than once.");

        var byName = draggables.ToDictionary(d => d.name);
        int rows = gm.gridOverlay.rows, cols = gm.gridOverlay.cols;

        var plan   = new List<(Draggable d, BoardLayoutEntry e)>();
        var owners = new Dictionary<(int row, int col), string>();
        foreach (var e in entries)
        {
            if (!byName.TryGetValue(e.objectName, out var d))
            {
                report.missing.Add(e.objectName);
                continue;
            }
            if (e.IsOnGrid)
            {
                if (e.row >= rows || e.col >= cols)
                {
                    report.outOfBounds.Add($"{e.objectName} ({e.row},{e.col})");
                    continue;
                }
                // Edit mode never stops two draggables sharing a cell, so a saved layout can
                // legitimately contain that. Apply it faithfully and flag it.
                if (owners.TryGetValue((e.row, e.col), out var other))
                    report.collisions.Add($"{other} & {e.objectName} at ({e.row},{e.col})");
                else
                    owners[(e.row, e.col)] = e.objectName;
            }
            plan.Add((d, e));
        }

        // The scene's draggables the layout doesn't mention stay where they are.
        foreach (var d in draggables)
            if (!listed.Contains(d.name)) report.unlisted.Add(d.name);

        if (strict && (report.missing.Count > 0 || report.outOfBounds.Count > 0))
        {
            string reason = report.missing.Count > 0
                ? $"'{report.missing[0]}' not found in scene (was it renamed?)."
                : $"{report.outOfBounds[0]} is outside the current grid.";
            return Abort(report, logTag, reason);
        }

        if (plan.Count == 0) return report;

        // Close whatever group is still open, so collapsing below cannot swallow the
        // user's previous edit into this one undo step.
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(undoLabel);
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var (d, e) in plan)
        {
            var sp = d.GetComponent<SolutionPosition>();
            Undo.RecordObject(d.transform, undoLabel);
            Undo.RecordObject(sp, undoLabel);
            if (e.IsOnGrid)
            {
                d.transform.position = gm.GetCellCenter(e.row, e.col);
                sp.solutionRow = e.row;
                sp.solutionCol = e.col;
            }
            else
            {
                d.transform.position = e.worldPosition;
                sp.solutionRow = -1;
                sp.solutionCol = -1;
            }
            EditorUtility.SetDirty(sp);
        }

        Undo.CollapseUndoOperations(undoGroup);
        gm.RefreshEditModeViolations();
        SceneView.RepaintAll();

        report.applied = plan.Count;
        return report;
    }

    private static ApplyReport Abort(ApplyReport report, string logTag, string reason)
    {
        report.aborted     = true;
        report.abortReason = reason;
        Debug.LogWarning($"[{logTag}] Cannot apply layout — {reason}");
        return report;
    }
}
