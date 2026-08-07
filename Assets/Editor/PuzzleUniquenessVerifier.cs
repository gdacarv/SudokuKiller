using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Brute-force verifies that the open scene's puzzle has exactly one rule-valid
/// layout and that the killer rules identify exactly one suspect in every valid layout.
/// </summary>
public static class PuzzleUniquenessVerifier
{
    const int MaxRecordedLayouts = 20;
    const int MaxRecordedPerKillerSet = 3;
    const long MaxVisits = 5_000_000;

    public enum Outcome { Unique, KillerUnique, NotUnique, Broken, Inconclusive, Error }

    [System.Serializable]
    public struct LayoutPlacement
    {
        public string suspect;
        public int row;
        public int col;
        public int section;
    }

    [System.Serializable]
    public class LayoutInfo
    {
        public List<LayoutPlacement> placements = new();
        public List<string> killers = new();
        public bool matchesAuthored;
    }

    [System.Serializable]
    public struct VerificationResult
    {
        public Outcome outcome;
        public string verdict;
        public string report;   // full text (summary + layout listings + verdict)
        public string summary;  // report without the per-layout listings
        public List<LayoutInfo> layouts;
    }

    // Verification rearranges live occupancy state, so it is Edit Mode only —
    // same guard the Level Rules window puts on its button.
    [MenuItem("Tools/Sudoku Killers/Verify Puzzle Uniqueness", true)]
    static bool VerifyValidate() => !Application.isPlaying;

    [MenuItem("Tools/Sudoku Killers/Verify Puzzle Uniqueness")]
    public static void Verify()
    {
        var result = Run();
        Debug.Log(result.report);
        System.IO.File.WriteAllText("Temp/puzzle_verifier_report.txt", result.report);
    }

    public static VerificationResult Run()
    {
        var gm = Object.FindFirstObjectByType<GridManager>();
        if (gm == null || gm.gridOverlay == null)
        {
            return new VerificationResult
            {
                outcome = Outcome.Error,
                verdict = "No GridManager/GridOverlay in scene.",
                report = "[PuzzleVerifier] No GridManager/GridOverlay in scene.",
                summary = "[PuzzleVerifier] No GridManager/GridOverlay in scene.",
                layouts = new List<LayoutInfo>(),
            };
        }

        // Build blocked/section maps, register scenery entities, and put every
        // draggable at its solution cell so we can snapshot solution state.
        gm.RefreshEditModeViolations();

        var draggables = Object.FindObjectsByType<Draggable>(FindObjectsSortMode.None)
            .Where(d => d.GetComponent<SolutionPosition>() != null)
            .OrderBy(d => d.name)
            .ToList();

        var originalCells = draggables.ToDictionary(
            d => d, d => new Vector2Int(d.Entity.Row, d.Entity.Col));

        // Take everyone off the board; scenery markers stay registered.
        foreach (var d in draggables)
        {
            gm.Release(d);
            d.Entity.Row = -1;
            d.Entity.Col = -1;
        }

        try
        {
            return RunSearch(gm, draggables, originalCells);
        }
        finally
        {
            // Restore solution state regardless of what happened.
            foreach (var kv in originalCells)
            {
                kv.Key.Entity.Row = kv.Value.x;
                kv.Key.Entity.Col = kv.Value.y;
            }
            gm.RefreshEditModeViolations();
        }
    }

    static VerificationResult RunSearch(GridManager gm, List<Draggable> draggables,
        Dictionary<Draggable, Vector2Int> originalCells)
    {
        int rows = gm.gridOverlay.rows, cols = gm.gridOverlay.cols;

        var freeCells = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (gm.IsCellAvailable(r, c))
                    freeCells.Add(new Vector2Int(r, c));

        var report = new StringBuilder();
        report.AppendLine($"[PuzzleVerifier] {draggables.Count} suspects, {freeCells.Count} free cells.");

        // Static candidates: cells passing every rule that no draggable can influence.
        var candidates = new Dictionary<Draggable, List<Vector2Int>>();
        foreach (var d in draggables)
        {
            var staticRules = d.rules.Where(r => r != null && !IsDynamic(r, draggables)).ToList();
            var cells = new List<Vector2Int>();
            foreach (var cell in freeCells)
            {
                d.Entity.Row = cell.x;
                d.Entity.Col = cell.y;
                if (staticRules.All(r => r.CanPlace(gm, d, cell.x, cell.y)))
                    cells.Add(cell);
            }
            d.Entity.Row = -1;
            d.Entity.Col = -1;
            candidates[d] = cells;
            int dynCount = d.rules.Count(r => r != null) - staticRules.Count;
            report.AppendLine($"  {d.name}: {cells.Count} static candidates ({staticRules.Count} static / {dynCount} dynamic rules)");
        }

        var order = draggables.OrderBy(d => candidates[d].Count).ToList();

        // Only monotone board rules may be used to prune partial assignments;
        // the rest are still enforced on every full assignment below.
        var pruneRules = gm.boardRules.Where(r => r != null && IsPruneSafe(r)).ToList();
        int deferred = gm.boardRules.Count(r => r != null) - pruneRules.Count;
        if (deferred > 0)
            report.AppendLine($"  ({deferred} board rule(s) can only be checked on complete layouts — search may be slower.)");

        var recordedLayouts = new List<LayoutInfo>();
        var recordedPerKillerSet = new Dictionary<string, int>();
        string KillerKey(List<string> names) => names.Count == 0 ? "(none)" : string.Join(",", names.OrderBy(k => k));
        var killerNames = new HashSet<string>();
        int noKillerLayouts = 0, multiKillerLayouts = 0;
        long visits = 0;
        int validCount = 0;
        bool aborted = false;

        void Dfs(int i)
        {
            if (aborted) return;
            if (++visits > MaxVisits) { aborted = true; return; }

            if (i == order.Count)
            {
                // Full assignment: evaluate every rule (incl. dynamic) + board rules.
                foreach (var d in order)
                {
                    int r = d.Entity.Row, c = d.Entity.Col;
                    if (!d.rules.Where(x => x != null).All(x => x.CanPlace(gm, d, r, c))) return;
                    if (!gm.CheckBoardRules(d, r, c)) return;
                }
                validCount++;

                // Killer analysis runs on EVERY valid layout (not just recorded
                // ones), so verdicts about "all layouts" are actually proven.
                var killers = order.Where(d => gm.EvaluateKillerRules(d) == null)
                                   .Select(d => d.name).ToList();
                if (killers.Count == 0) noKillerLayouts++;
                else if (killers.Count > 1) multiKillerLayouts++;
                killerNames.UnionWith(killers);

                // Cap how many layouts we record per killer-set so the sample shown
                // to the user has variety instead of being dominated by whichever
                // killer the DFS happens to reach first.
                string killerKey = KillerKey(killers);
                bool isAuthoredSolution = order.All(d => originalCells[d] == new Vector2Int(d.Entity.Row, d.Entity.Col));
                recordedPerKillerSet.TryGetValue(killerKey, out int recordedForKiller);

                // The layout currently on the board must always be one of the
                // recorded options for its killer set, even if the per-killer
                // (or overall) cap was already filled by layouts found earlier.
                if (isAuthoredSolution && (recordedForKiller >= MaxRecordedPerKillerSet || recordedLayouts.Count >= MaxRecordedLayouts))
                {
                    int evictIndex = recordedLayouts.FindLastIndex(l => KillerKey(l.killers) == killerKey);
                    if (evictIndex < 0) evictIndex = recordedLayouts.Count - 1;
                    recordedPerKillerSet[KillerKey(recordedLayouts[evictIndex].killers)]--;
                    recordedLayouts.RemoveAt(evictIndex);
                    recordedForKiller = recordedPerKillerSet.TryGetValue(killerKey, out int rc) ? rc : 0;
                }

                if (recordedLayouts.Count < MaxRecordedLayouts && recordedForKiller < MaxRecordedPerKillerSet)
                {
                    recordedPerKillerSet[killerKey] = recordedForKiller + 1;
                    var info = new LayoutInfo { killers = killers, matchesAuthored = isAuthoredSolution };
                    foreach (var d in order.OrderBy(x => x.name))
                    {
                        info.placements.Add(new LayoutPlacement
                        {
                            suspect = d.name,
                            row = d.Entity.Row,
                            col = d.Entity.Col,
                            section = gm.GetSection(d.Entity.Row, d.Entity.Col),
                        });
                    }
                    // The current-board layout leads the list (and its killer-set
                    // group) rather than landing wherever the DFS happened to find it.
                    if (isAuthoredSolution)
                        recordedLayouts.Insert(0, info);
                    else
                        recordedLayouts.Add(info);
                }
                return;
            }

            var d2 = order[i];
            foreach (var cell in candidates[d2])
            {
                if (!gm.TryPlace(d2, cell.x, cell.y)) continue;
                d2.Entity.Row = cell.x;
                d2.Entity.Col = cell.y;

                if (pruneRules.All(r => r.CanPlace(gm, d2, cell.x, cell.y)))
                    Dfs(i + 1);

                gm.Release(d2);
                d2.Entity.Row = -1;
                d2.Entity.Col = -1;
            }
        }

        Dfs(0);

        report.AppendLine(aborted
            ? $"ABORTED after {MaxVisits:N0} visits — search space too large, add more static clues."
            : $"Search complete: {validCount} valid layout(s), {visits:N0} nodes visited.");

        if (validCount > 0)
            report.AppendLine($"Killer analysis over all {validCount} layout(s): "
                + $"{noKillerLayouts} with no killer, {multiKillerLayouts} with multiple killers, "
                + $"killers seen: [{string.Join(", ", killerNames)}]");
        if (validCount > recordedLayouts.Count)
            report.AppendLine($"(showing first {recordedLayouts.Count} layouts)");

        var layoutsText = new StringBuilder();
        foreach (var info in recordedLayouts)
        {
            layoutsText.AppendLine($"  Layout: killer(s) = [{string.Join(", ", info.killers)}]"
                + (info.matchesAuthored ? " (matches authored solution)" : ""));
            foreach (var p in info.placements)
                layoutsText.AppendLine($"    {p.suspect}: ({p.row},{p.col}) section {p.section}");
        }

        bool killerAlwaysUnique = noKillerLayouts == 0 && multiKillerLayouts == 0;

        Outcome outcome;
        string verdict;
        if (aborted)
        {
            outcome = Outcome.Inconclusive;
            verdict = "INCONCLUSIVE (aborted): search space too large — add more static clues";
        }
        else if (validCount == 0)
        {
            outcome = Outcome.Broken;
            verdict = "BROKEN: no valid layout exists";
        }
        else if (validCount == 1 && killerAlwaysUnique)
        {
            outcome = Outcome.Unique;
            verdict = $"UNIQUE: 1 layout, killer = {killerNames.First()}";
        }
        else if (validCount == 1)
        {
            outcome = Outcome.Broken;
            verdict = noKillerLayouts > 0
                ? "BROKEN: 1 layout but nobody satisfies the killer rules"
                : $"BROKEN: 1 layout but multiple killers qualify: [{string.Join(", ", killerNames)}]";
        }
        else if (killerAlwaysUnique && killerNames.Count == 1)
        {
            outcome = Outcome.KillerUnique;
            verdict = $"KILLER-UNIQUE (acceptable): {validCount} layouts, but every layout leads to killer = {killerNames.First()}";
        }
        else
        {
            outcome = Outcome.NotUnique;
            var problems = new List<string>();
            if (noKillerLayouts > 0) problems.Add($"{noKillerLayouts} layout(s) with NO killer");
            if (multiKillerLayouts > 0) problems.Add($"{multiKillerLayouts} layout(s) with multiple killers");
            if (killerNames.Count > 1) problems.Add($"different killers across layouts: [{string.Join(", ", killerNames)}]");
            verdict = $"NOT UNIQUE: {validCount} layouts — {string.Join("; ", problems)}";
        }

        string summary = report.ToString() + "VERDICT: " + verdict;
        string fullReport = report.ToString() + layoutsText + "VERDICT: " + verdict;
        return new VerificationResult
        {
            outcome = outcome,
            verdict = verdict,
            report = fullReport,
            summary = summary,
            layouts = recordedLayouts,
        };
    }

    /// <summary>
    /// A board rule may be used to prune a partial assignment only if failing now
    /// guarantees failing at every completion. Counting rules only ever count UP as
    /// suspects land, so an upper bound ("fewer than n", or its n=0 EqualTo spelling)
    /// can never recover — while EqualTo n&gt;0 and GreaterThan are legitimately
    /// unsatisfied mid-search and must wait for a complete layout.
    /// </summary>
    static bool IsPruneSafe(Rule rule) => rule switch
    {
        NPerRowRule r     => IsUpperBound(r.comparison, r.n),
        NPerColumnRule r  => IsUpperBound(r.comparison, r.n),
        NPerSectionRule r => IsUpperBound(r.comparison, r.n),
        _ => false,
    };

    static bool IsUpperBound(ComparisonMode mode, int n)
        => mode == ComparisonMode.LessThan || (mode == ComparisonMode.EqualTo && n == 0);

    /// <summary>A rule is dynamic if its outcome can depend on where other draggables are placed.</summary>
    static bool IsDynamic(Rule rule, List<Draggable> draggables)
    {
        if (rule is NPerRowRule || rule is NPerColumnRule || rule is NPerSectionRule)
            return true;

        List<GridEntity.TagEntry> targets = rule switch
        {
            DistanceToTagRule dt => dt.targetTags,
            PositionObjectRule po => po.targetTags,
            SameSectionAsTagRule ss => ss.targetTags,
            _ => null,
        };
        if (targets == null) return false; // TagRule, InSectionRule, RequireTaggedCellRule

        return draggables.Any(d => d.Entity != null && d.Entity.MatchesAll(targets));
    }
}
