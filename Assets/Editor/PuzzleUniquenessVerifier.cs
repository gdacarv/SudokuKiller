using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Brute-force verifies that the open scene's puzzle has exactly one rule-valid
/// layout and that the killer rules identify exactly one suspect in every valid layout.
///
/// The search is a backtracking DFS with forward checking and dynamic most-constrained-variable
/// ordering. By default it enumerates EVERY valid layout, so counts and the killer analysis are exact;
/// the opt-in <see cref="Options.stopWhenNotUnique"/> ends the search early once a NOT UNIQUE verdict
/// is already forced (counts then become lower bounds). Rule semantics live only in <see cref="Rule.CanPlace"/>; the solver merely
/// decides WHEN a rule's verdict is final enough to prune on (see <see cref="CollectDependencies"/>).
///
/// It is bounded three ways so it can never hang the Editor: a node budget, a wall-clock budget, and
/// (when interactive) a cancelable progress bar. Scene state is restored on every exit path.
/// </summary>
public static class PuzzleUniquenessVerifier
{
    // Defaults for the user-adjustable limits (see Options; the Level Rules window edits them).
    public const int DefaultMaxRecordedLayouts = 20;
    public const int DefaultMaxRecordedPerKillerSet = 5;
    public const long DefaultMaxVisits = 5_000_000;
    public const double DefaultMaxSeconds = 40.0;

    /// <summary>
    /// Limits that bound a run. Defaults are the constants above; the Level Rules window lets the user
    /// change them before verifying, and persists them (EditorPrefs) so the menu item uses the same values.
    /// Values are clamped by <see cref="Sanitized"/> so no setting can turn the verifier back into a hang.
    /// </summary>
    [System.Serializable]
    public struct Options
    {
        /// <summary>Wall-clock budget. Exceeding it yields Outcome.Inconclusive.</summary>
        public double maxSeconds;
        /// <summary>Node budget: placement attempts the search may make before giving up.</summary>
        public long maxVisits;
        /// <summary>How many valid layouts are kept for display. Counting and killer analysis still cover ALL of them.</summary>
        public int maxRecordedLayouts;
        /// <summary>Per killer-set cap on kept layouts, so the sample shows variety instead of one killer's layouts.</summary>
        public int maxRecordedPerKillerSet;
        /// <summary>
        /// Opt-in early exit: stop as soon as the NOT UNIQUE verdict is already forced (2+ layouts and a killer
        /// problem — none, several, or differing killers). Sound, since those conditions only grow with more
        /// layouts. UNIQUE / KILLER-UNIQUE / BROKEN still need the full search. Trade-off: counts become
        /// lower bounds and fewer layouts are listed.
        /// </summary>
        public bool stopWhenNotUnique;

        public const double MinSeconds = 1.0;
        public const long MinVisits = 1_000;
        public const int MinRecorded = 1;
        // Every kept layout is one row of IMGUI in the Level Rules window; thousands would freeze the Editor.
        public const int MaxRecordedCap = 500;

        public static Options Default => new Options
        {
            maxSeconds = DefaultMaxSeconds,
            maxVisits = DefaultMaxVisits,
            maxRecordedLayouts = DefaultMaxRecordedLayouts,
            maxRecordedPerKillerSet = DefaultMaxRecordedPerKillerSet,
            stopWhenNotUnique = false,
        };

        public Options Sanitized() => new Options
        {
            maxSeconds = System.Math.Max(MinSeconds, double.IsNaN(maxSeconds) ? DefaultMaxSeconds : maxSeconds),
            maxVisits = System.Math.Max(MinVisits, maxVisits),
            maxRecordedLayouts = System.Math.Clamp(maxRecordedLayouts, MinRecorded, MaxRecordedCap),
            maxRecordedPerKillerSet = System.Math.Clamp(maxRecordedPerKillerSet, MinRecorded, MaxRecordedCap),
            stopWhenNotUnique = stopWhenNotUnique,
        };

        public bool IsDefault
        {
            get
            {
                var s = Sanitized();
                var d = Default;
                return s.maxSeconds == d.maxSeconds && s.maxVisits == d.maxVisits
                    && s.maxRecordedLayouts == d.maxRecordedLayouts
                    && s.maxRecordedPerKillerSet == d.maxRecordedPerKillerSet
                    && s.stopWhenNotUnique == d.stopWhenNotUnique;
            }
        }

        const string PrefPrefix = "SudokuKillers.PuzzleVerifier.";

        public static Options Load()
        {
            var d = Default;
            if (!long.TryParse(EditorPrefs.GetString(PrefPrefix + "maxVisits", ""),
                    System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long visits))
                visits = d.maxVisits; // unset or corrupt pref: fall back to the default, not to TryParse's 0
            return new Options
            {
                maxSeconds = EditorPrefs.GetFloat(PrefPrefix + "maxSeconds", (float)d.maxSeconds),
                maxVisits = visits,
                maxRecordedLayouts = EditorPrefs.GetInt(PrefPrefix + "maxRecordedLayouts", d.maxRecordedLayouts),
                maxRecordedPerKillerSet = EditorPrefs.GetInt(PrefPrefix + "maxRecordedPerKillerSet", d.maxRecordedPerKillerSet),
                stopWhenNotUnique = EditorPrefs.GetBool(PrefPrefix + "stopWhenNotUnique", d.stopWhenNotUnique),
            }.Sanitized();
        }

        public void Save()
        {
            var s = Sanitized();
            EditorPrefs.SetFloat(PrefPrefix + "maxSeconds", (float)s.maxSeconds);
            EditorPrefs.SetString(PrefPrefix + "maxVisits", s.maxVisits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorPrefs.SetInt(PrefPrefix + "maxRecordedLayouts", s.maxRecordedLayouts);
            EditorPrefs.SetInt(PrefPrefix + "maxRecordedPerKillerSet", s.maxRecordedPerKillerSet);
            EditorPrefs.SetBool(PrefPrefix + "stopWhenNotUnique", s.stopWhenNotUnique);
        }
    }

    const string ProgressTitle = "Verify Puzzle Uniqueness";
    const int BudgetCheckMask = 63;          // check clock/cancel every 64 placement attempts
    const long ProgressIntervalMs = 100;     // never repaint the progress bar more often than this

    // Cancelled is appended last: Outcome is serialized inside LevelRulesWindow's cached result.
    public enum Outcome { Unique, KillerUnique, NotUnique, Broken, Inconclusive, Error, Cancelled }

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
        var result = Run(Options.Load(), interactive: true);
        Debug.Log(result.report);
        System.IO.File.WriteAllText("Temp/puzzle_verifier_report.txt", result.report);
    }

    /// <summary>Programmatic entry point: default limits except the wall-clock budget.</summary>
    /// <param name="interactive">Show a cancelable progress bar. Never opens a modal dialog, so it is
    /// safe for the UnityMCP bridge either way; pass false for headless/programmatic runs.</param>
    /// <param name="maxSeconds">Wall-clock budget. Exceeding it yields Outcome.Inconclusive.</param>
    public static VerificationResult Run(bool interactive = false, double maxSeconds = DefaultMaxSeconds)
    {
        var options = Options.Default;
        options.maxSeconds = maxSeconds;
        return Run(options, interactive);
    }

    /// <param name="options">Run limits; sanitized (clamped) before use.</param>
    /// <param name="interactive">See the overload above.</param>
    public static VerificationResult Run(Options options, bool interactive = false)
    {
        options = options.Sanitized();
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
            // Tags and registrations are frozen for the whole search (only positions move), so tag
            // queries can be memoized. Closed in the finally below, before the scene is rebuilt.
            gm.BeginTagQueryCache();
            return RunSearch(gm, draggables, originalCells, interactive, options);
        }
        finally
        {
            gm.EndTagQueryCache();
            EditorUtility.ClearProgressBar();

            // Restore solution state regardless of what happened.
            foreach (var kv in originalCells)
            {
                kv.Key.Entity.Row = kv.Value.x;
                kv.Key.Entity.Col = kv.Value.y;
            }
            gm.RefreshEditModeViolations();
        }
    }

    enum AbortReason { None, Visits, Time, Cancelled }

    static VerificationResult RunSearch(GridManager gm, List<Draggable> draggables,
        Dictionary<Draggable, Vector2Int> originalCells, bool interactive, Options options)
    {
        int rows = gm.gridOverlay.rows, cols = gm.gridOverlay.cols;

        var freeCells = new List<int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (gm.IsCellAvailable(r, c))
                    freeCells.Add(r * cols + c);

        var report = new StringBuilder();
        report.AppendLine($"[PuzzleVerifier] {draggables.Count} suspects, {freeCells.Count} free cells.");
        // Only when customized: default runs keep byte-identical reports, so they stay diffable across versions.
        if (!options.IsDefault)
            report.AppendLine($"  Options: {options.maxSeconds:0.#}s budget, {options.maxVisits:N0} node budget, "
                + $"keep up to {options.maxRecordedLayouts} layout(s) ({options.maxRecordedPerKillerSet} per killer set)"
                + (options.stopWhenNotUnique ? ", stop early once NOT UNIQUE is proven." : "."));

        var solver = new Solver(gm, draggables, originalCells, rows, cols, freeCells, report, interactive, options);
        solver.Prepare();
        solver.Search();

        int validCount = solver.validCount;
        int noKillerLayouts = solver.noKillerLayouts, multiKillerLayouts = solver.multiKillerLayouts;
        // Sorted so reports are stable run-to-run and diffable (insertion order follows DFS order).
        var killerNames = new SortedSet<string>(solver.killerNames, System.StringComparer.Ordinal);
        var recordedLayouts = solver.recordedLayouts;
        bool aborted = solver.abort != AbortReason.None;
        bool cancelled = solver.abort == AbortReason.Cancelled;
        bool stoppedEarly = solver.stoppedEarly;

        if (aborted)
            solver.AppendAbortDiagnostics();
        else if (stoppedEarly)
            report.AppendLine($"Stopped early: NOT UNIQUE already proven after {validCount} layout(s), {solver.visits:N0} nodes in {solver.ElapsedSeconds:0.0}s "
                + "— the remaining layouts were not enumerated, so counts below are lower bounds.");
        else
            report.AppendLine($"Search complete: {validCount} valid layout(s), {solver.visits:N0} nodes visited in {solver.ElapsedSeconds:0.0}s.");

        if (validCount > 0)
            report.AppendLine($"Killer analysis over {(aborted ? "the " + validCount + " layout(s) found before the search stopped (INCOMPLETE)" : stoppedEarly ? "the " + validCount + " layout(s) found before stopping early (lower bounds)" : "all " + validCount + " layout(s)")}: "
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
        if (cancelled)
        {
            outcome = Outcome.Cancelled;
            verdict = $"CANCELLED: stopped before completion ({validCount} layout(s) found so far) — result is not conclusive";
        }
        else if (aborted)
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
            verdict = stoppedEarly
                ? $"NOT UNIQUE: at least {validCount} layouts — {string.Join("; ", problems)} (stopped early; layouts not fully enumerated)"
                : $"NOT UNIQUE: {validCount} layouts — {string.Join("; ", problems)}";
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
    /// Backtracking search over "which cell does each suspect stand on".
    ///
    /// Every rule that applies to a suspect (its own clues plus the board rules) is given a dependency
    /// set: the OTHER suspects whose placement can change its verdict. A rule may prune as soon as
    ///   (a) its verdict is monotone (<see cref="IsPruneSafe"/>: false now ⇒ false forever), or
    ///   (b) every suspect in its dependency set is already placed (its verdict is then final).
    /// Anything else waits — which is what keeps non-monotone rules (e.g. "no target on the grid ⇒ false",
    /// which flips once the target lands) from ever pruning a valid layout.
    ///
    /// When suspect X lands, every rule depending on X that has become prunable is applied in BOTH
    /// directions: it filters the live domain of each still-unplaced owner (forward check) and is
    /// re-verified for each already-placed owner. The full rule set is still re-checked at every leaf
    /// as a correctness backstop.
    /// </summary>
    sealed class Solver
    {
        struct RuleRef { public int owner, rule; }

        readonly GridManager gm;
        readonly List<Draggable> allDraggables;
        public readonly Draggable[] ds;
        readonly int n, rows, cols;
        readonly Dictionary<Draggable, Vector2Int> originalCells;
        readonly List<int> freeCells;
        readonly StringBuilder report;
        readonly bool interactive;
        readonly Options opt;
        readonly long maxMs;
        readonly double maxSeconds;

        // Per suspect: own clues first (ownCount), then the board rules.
        Rule[][] rules;
        bool[][] safe;          // monotone => may prune before its dependencies are all placed
        int[][][] deps;         // OTHER suspects (indices) whose position can change the verdict
        int[] ownCount;
        int[] staticOwn, dynamicOwn;
        List<RuleRef>[] affected;   // affected[x] = rules whose verdict can change when x is placed

        // Domains as sparse sets over cell indices (row * cols + col): dom[i][0..size[i]) is live.
        // Removal swaps to the end and shrinks size; undo is just restoring size (see saved).
        int[][] dom, pos;
        int[] size, initSize;
        int[][] saved;
        bool[] placed;

        // Results
        public long visits;
        public int validCount, noKillerLayouts, multiKillerLayouts;
        public readonly HashSet<string> killerNames = new();
        public readonly List<LayoutInfo> recordedLayouts = new();
        readonly Dictionary<string, int> recordedPerKillerSet = new();

        public AbortReason abort;
        public bool stoppedEarly;   // opt-in: NOT UNIQUE was proven before the search finished (not an abort)
        bool authoredSeeded;        // authored layout already counted up front; skip it if the DFS reaches it again
        bool Halted => abort != AbortReason.None || stoppedEarly;
        int deepest;
        double rootFraction;
        System.Diagnostics.Stopwatch clock;
        long lastProgressMs;

        public double ElapsedSeconds => clock == null ? 0 : clock.Elapsed.TotalSeconds;

        public Solver(GridManager gm, List<Draggable> draggables, Dictionary<Draggable, Vector2Int> originalCells,
            int rows, int cols, List<int> freeCells, StringBuilder report, bool interactive, Options options)
        {
            this.gm = gm;
            allDraggables = draggables;
            ds = draggables.ToArray();
            n = ds.Length;
            this.rows = rows;
            this.cols = cols;
            this.originalCells = originalCells;
            this.freeCells = freeCells;
            this.report = report;
            this.interactive = interactive && !Application.isBatchMode;
            opt = options;
            maxSeconds = options.maxSeconds;
            maxMs = (long)(maxSeconds * 1000.0);
        }

        // ── Setup ─────────────────────────────────────────────────────────

        public void Prepare()
        {
            var indexOf = new Dictionary<Draggable, int>();
            for (int i = 0; i < n; i++) indexOf[ds[i]] = i;

            var boardRules = gm.boardRules.Where(r => r != null).ToList();

            rules = new Rule[n][];
            safe = new bool[n][];
            deps = new int[n][][];
            ownCount = new int[n];
            staticOwn = new int[n];
            dynamicOwn = new int[n];
            affected = new List<RuleRef>[n];
            for (int i = 0; i < n; i++) affected[i] = new List<RuleRef>();

            var leafOnly = new SortedSet<string>();
            for (int i = 0; i < n; i++)
            {
                var own = ds[i].rules.Where(r => r != null).ToList();
                ownCount[i] = own.Count;
                var all = own.Concat(boardRules).ToArray();
                rules[i] = all;
                safe[i] = new bool[all.Length];
                deps[i] = new int[all.Length][];

                for (int j = 0; j < all.Length; j++)
                {
                    safe[i][j] = IsPruneSafe(all[j]);

                    var set = new HashSet<Draggable>();
                    CollectDependencies(all[j], allDraggables, set);
                    set.Remove(ds[i]); // the owner's own cell is always the (row, col) being tested
                    var idx = set.Select(d => indexOf[d]).OrderBy(x => x).ToArray();
                    deps[i][j] = idx;

                    foreach (int x in idx)
                        affected[x].Add(new RuleRef { owner = i, rule = j });

                    if (j < ownCount[i])
                    {
                        if (idx.Length == 0) staticOwn[i]++; else dynamicOwn[i]++;
                    }

                    if (!safe[i][j] && n > 1 && idx.Length == n - 1)
                        leafOnly.Add(j < ownCount[i] ? $"{ds[i].name}: {all[j].name}" : $"board: {all[j].name}");
                }
            }

            // Initial domains: cells passing every rule that no other suspect can influence.
            dom = new int[n][];
            pos = new int[n][];
            size = new int[n];
            initSize = new int[n];
            placed = new bool[n];
            saved = new int[n + 1][];
            for (int i = 0; i <= n; i++) saved[i] = new int[n];

            for (int i = 0; i < n; i++)
            {
                var d = ds[i];
                var cells = new List<int>();
                foreach (int cell in freeCells)
                {
                    if (AllStaticRulesPass(i, cell))
                        cells.Add(cell);
                }
                d.Entity.Row = -1;
                d.Entity.Col = -1;

                dom[i] = cells.ToArray();
                size[i] = initSize[i] = dom[i].Length;
                pos[i] = new int[rows * cols];
                for (int k = 0; k < pos[i].Length; k++) pos[i][k] = int.MaxValue;
                for (int k = 0; k < dom[i].Length; k++) pos[i][dom[i][k]] = k;

                report.AppendLine($"  {d.name}: {size[i]} static candidates ({staticOwn[i]} static / {dynamicOwn[i]} dynamic rules)");
            }

            if (leafOnly.Count > 0)
                report.AppendLine($"  ({leafOnly.Count} rule(s) can only be checked once every other suspect is placed — search may be slower: "
                    + string.Join(", ", leafOnly.Take(8)) + (leafOnly.Count > 8 ? ", …" : "") + ")");
        }

        bool AllStaticRulesPass(int i, int cell)
        {
            var d = ds[i];
            int r = cell / cols, c = cell % cols;
            d.Entity.Row = r;
            d.Entity.Col = c;
            for (int j = 0; j < rules[i].Length; j++)
                if (deps[i][j].Length == 0 && !rules[i][j].CanPlace(gm, d, r, c))
                    return false;
            return true;
        }

        // ── Search ────────────────────────────────────────────────────────

        public void Search()
        {
            clock = System.Diagnostics.Stopwatch.StartNew();
            lastProgressMs = 0;

            // Early exit may end the search before the DFS reaches the layout currently on the board, and that
            // layout must always be listed (the window's "matches authored solution" marker relies on it).
            // So evaluate it first; the DFS then skips it if it comes across it again.
            if (opt.stopWhenNotUnique)
                SeedAuthoredLayout();

            Dfs(0);
        }

        void SeedAuthoredLayout()
        {
            var placedHere = new List<(Draggable d, int r, int c)>();
            bool ok = true;
            foreach (var d in ds)
            {
                var cell = originalCells[d];
                if (cell.x < 0 || cell.y < 0 || !gm.TryPlace(d, cell.x, cell.y)) { ok = false; break; }
                d.Entity.Row = cell.x;
                d.Entity.Col = cell.y;
                placedHere.Add((d, cell.x, cell.y));
            }

            if (ok)
            {
                int before = validCount;
                Leaf();                              // full rule check, counting and recording, as for any layout
                authoredSeeded = validCount > before; // only skip it later if it was actually valid and counted
            }

            foreach (var (d, r, c) in placedHere)
            {
                gm.Release(d, r, c);
                d.Entity.Row = -1;
                d.Entity.Col = -1;
            }
        }

        bool OutOfBudget()
        {
            long ms = clock.ElapsedMilliseconds;
            if (ms > maxMs) { abort = AbortReason.Time; return true; }

            if (interactive && ms - lastProgressMs >= ProgressIntervalMs)
            {
                lastProgressMs = ms;
                bool cancel = EditorUtility.DisplayCancelableProgressBar(ProgressTitle,
                    $"{visits:N0} nodes · {validCount} layout(s) · depth {deepest}/{n} · {ms / 1000.0:0.0}s of {maxSeconds:0.#}s",
                    (float)System.Math.Min(1.0, System.Math.Max(0.0, rootFraction)));
                if (cancel) { abort = AbortReason.Cancelled; return true; }
            }
            return false;
        }

        void Dfs(int depth)
        {
            if (Halted) return;
            if (depth > deepest) deepest = depth;

            if (depth == n) { Leaf(); return; }

            // Dynamic most-constrained variable: the unplaced suspect with the fewest live cells.
            int x = -1, best = int.MaxValue;
            for (int i = 0; i < n; i++)
                if (!placed[i] && size[i] < best) { best = size[i]; x = i; }

            System.Array.Copy(size, saved[depth], n);
            int count = size[x];
            var d = ds[x];

            for (int k = 0; k < count; k++)
            {
                if (depth == 0) rootFraction = (double)k / count;

                if (++visits > opt.maxVisits) { abort = AbortReason.Visits; return; }
                if ((visits & BudgetCheckMask) == 0 && OutOfBudget()) return;

                int cell = dom[x][k];
                int r = cell / cols, c = cell % cols;
                if (!gm.TryPlace(d, r, c)) continue;
                d.Entity.Row = r;
                d.Entity.Col = c;
                placed[x] = true;

                if (Propagate(x, cell))
                    Dfs(depth + 1);

                // Undo: domains back to what they were when this node was entered.
                System.Array.Copy(saved[depth], size, n);
                placed[x] = false;
                gm.Release(d, r, c);
                d.Entity.Row = -1;
                d.Entity.Col = -1;

                if (Halted) return;
            }
        }

        /// <summary>Applies everything that placing x on 'cell' newly decides. False = dead end.</summary>
        bool Propagate(int x, int cell)
        {
            // Cells are exclusive: nobody else may use this one.
            for (int i = 0; i < n; i++)
            {
                if (placed[i] || pos[i][cell] >= size[i]) continue;
                Remove(i, cell);
                if (size[i] == 0) return false;
            }

            foreach (var rr in affected[x])
            {
                int i = rr.owner, j = rr.rule;
                if (!safe[i][j] && !AllPlaced(deps[i][j])) continue;

                if (placed[i])
                {
                    var d = ds[i];
                    if (!rules[i][j].CanPlace(gm, d, d.Entity.Row, d.Entity.Col)) return false;
                }
                else
                {
                    FilterDomain(i, rules[i][j]);
                    if (size[i] == 0) return false;
                }
            }
            return true;
        }

        bool AllPlaced(int[] indices)
        {
            for (int k = 0; k < indices.Length; k++)
                if (!placed[indices[k]]) return false;
            return true;
        }

        /// <summary>Drops every live cell of unplaced suspect i for which 'rule' fails. Same probing as the static pass.</summary>
        void FilterDomain(int i, Rule rule)
        {
            var d = ds[i];
            var cells = dom[i];
            for (int k = size[i] - 1; k >= 0; k--)
            {
                int cell = cells[k];
                int r = cell / cols, c = cell % cols;
                d.Entity.Row = r;
                d.Entity.Col = c;
                if (!rule.CanPlace(gm, d, r, c))
                    RemoveAt(i, k);
            }
            d.Entity.Row = -1;
            d.Entity.Col = -1;
        }

        void Remove(int i, int cell) => RemoveAt(i, pos[i][cell]);

        void RemoveAt(int i, int p)
        {
            int last = --size[i];
            int moved = dom[i][last];
            int removed = dom[i][p];
            dom[i][p] = moved;
            pos[i][moved] = p;
            dom[i][last] = removed;
            pos[i][removed] = last;
        }

        // ── Leaf ──────────────────────────────────────────────────────────

        void Leaf()
        {
            // Backstop: full assignment, evaluate every rule (incl. dynamic) + board rules from scratch,
            // independent of any pruning decision made on the way down.
            foreach (var d in ds)
            {
                int r = d.Entity.Row, c = d.Entity.Col;
                if (!d.rules.Where(x => x != null).All(x => x.CanPlace(gm, d, r, c))) return;
                if (!gm.CheckBoardRules(d, r, c)) return;
            }

            bool isAuthoredSolution = ds.All(d => originalCells[d] == new Vector2Int(d.Entity.Row, d.Entity.Col));
            if (authoredSeeded && isAuthoredSolution) return; // already counted by SeedAuthoredLayout

            validCount++;

            // Killer analysis runs on EVERY valid layout (not just recorded
            // ones), so verdicts about "all layouts" are actually proven.
            var killers = ds.Where(d => gm.EvaluateKillerRules(d) == null)
                            .Select(d => d.name).ToList();
            if (killers.Count == 0) noKillerLayouts++;
            else if (killers.Count > 1) multiKillerLayouts++;
            killerNames.UnionWith(killers);

            // Cap how many layouts we record per killer-set so the sample shown
            // to the user has variety instead of being dominated by whichever
            // killer the DFS happens to reach first.
            string killerKey = KillerKey(killers);
            recordedPerKillerSet.TryGetValue(killerKey, out int recordedForKiller);

            // The layout currently on the board must always be one of the
            // recorded options for its killer set, even if the per-killer
            // (or overall) cap was already filled by layouts found earlier.
            if (isAuthoredSolution && (recordedForKiller >= opt.maxRecordedPerKillerSet || recordedLayouts.Count >= opt.maxRecordedLayouts))
            {
                int evictIndex = recordedLayouts.FindLastIndex(l => KillerKey(l.killers) == killerKey);
                if (evictIndex < 0) evictIndex = recordedLayouts.Count - 1;
                recordedPerKillerSet[KillerKey(recordedLayouts[evictIndex].killers)]--;
                recordedLayouts.RemoveAt(evictIndex);
                recordedForKiller = recordedPerKillerSet.TryGetValue(killerKey, out int rc) ? rc : 0;
            }

            if (recordedLayouts.Count < opt.maxRecordedLayouts && recordedForKiller < opt.maxRecordedPerKillerSet)
            {
                recordedPerKillerSet[killerKey] = recordedForKiller + 1;
                var info = new LayoutInfo { killers = killers, matchesAuthored = isAuthoredSolution };
                foreach (var d in ds) // already name-sorted
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

            // Opt-in early exit. These conditions are exactly the ones that make the final verdict NOT UNIQUE
            // (see the verdict chain in RunSearch), and every term only ever grows as more layouts are found,
            // so stopping here cannot change the verdict — only the counts, which become lower bounds.
            if (opt.stopWhenNotUnique && validCount >= 2
                && (noKillerLayouts > 0 || multiKillerLayouts > 0 || killerNames.Count > 1))
                stoppedEarly = true;
        }

        static string KillerKey(List<string> names)
            => names.Count == 0 ? "(none)" : string.Join(",", names.OrderBy(k => k));

        // ── Diagnostics ───────────────────────────────────────────────────

        public void AppendAbortDiagnostics()
        {
            string why = abort switch
            {
                AbortReason.Time => $"time budget of {maxSeconds:0.#}s",
                AbortReason.Visits => $"node budget of {opt.maxVisits:N0}",
                _ => "user cancel",
            };
            report.AppendLine($"{(abort == AbortReason.Cancelled ? "CANCELLED" : "ABORTED")} ({why}) after {visits:N0} nodes in {ElapsedSeconds:0.0}s, "
                + $"deepest depth {deepest}/{n}. {validCount} valid layout(s) found so far — verdict NOT proven.");

            double space = 1;
            for (int i = 0; i < n; i++) space *= System.Math.Max(1, initSize[i]);
            report.AppendLine($"Raw space after static filtering (product of candidate counts): {space:0.0e+0}");

            var worst = Enumerable.Range(0, n).OrderByDescending(i => initSize[i]).ThenBy(i => ds[i].name).Take(3).ToList();
            report.AppendLine("Widest suspects (most candidate cells):");
            foreach (int i in worst)
                report.AppendLine($"  {ds[i].name}: {initSize[i]} cells ({staticOwn[i]} static / {dynamicOwn[i]} dynamic clues)");
            if (worst.Count > 0)
                report.AppendLine($"Suggest: add a static clue (tag, section, or tagged-cell rule) to {ds[worst[0]].name}.");
        }
    }

    /// <summary>
    /// A rule may be used to prune a partial assignment only if failing now
    /// guarantees failing at every completion. Counting rules only ever count UP as
    /// suspects land, so an upper bound ("fewer than n", or its n=0 EqualTo spelling)
    /// can never recover — while EqualTo n&gt;0 and GreaterThan are legitimately
    /// unsatisfied mid-search and must wait until every suspect the rule depends on is placed.
    ///
    /// RegionSumRule is deliberately absent here: an upper-bound sum is only prune-safe if every
    /// summed tag value is non-negative, and that can't be verified from the Rule asset alone (the
    /// values live on arbitrary scene entities). Excluding it just costs search speed; including it
    /// wrongly could silently prune away valid layouts, which is the exact failure mode this file
    /// exists to prevent — so when in doubt, leave a rule out of this switch.
    /// </summary>
    static bool IsPruneSafe(Rule rule) => rule switch
    {
        NPerRowRule r        => IsUpperBound(r.comparison, r.n),
        NPerColumnRule r     => IsUpperBound(r.comparison, r.n),
        NPerSectionRule r    => IsUpperBound(r.comparison, r.n),
        NeighborCountRule nc => IsUpperBound(nc.comparison, nc.n),
        UniqueByTagKeyRule   => true, // a duplicate, once created, can never be un-created by further placements
        AnyOfRule any        => any.options.Count > 0 && any.options.All(r => r != null && IsPruneSafe(r)),
        // AND: if it's currently false, at least one prune-safe child is false and stays false forever.
        AllOfRule all        => all.options.All(r => r != null && IsPruneSafe(r)),
        _ => false,
    };

    static bool IsUpperBound(ComparisonMode mode, int n)
        => mode == ComparisonMode.LessThan || (mode == ComparisonMode.EqualTo && n == 0);

    /// <summary>
    /// Adds to 'acc' every searched draggable whose position (or on-grid status) can change this rule's
    /// verdict. Once all of them are placed the verdict is final. Must err on the side of MORE: an
    /// under-approximation would let the rule prune before it is decided, silently discarding valid
    /// layouts. Unknown rule types therefore depend on everyone, which just defers them to the last placement.
    /// </summary>
    static void CollectDependencies(Rule rule, List<Draggable> all, HashSet<Draggable> acc)
    {
        switch (rule)
        {
            case null:
                return; // empty inspector slot — a common authoring slip, no dependencies

            // Only the owner's own tags / cell: nothing else can change the verdict.
            case TagRule:
            case InSectionRule:
                return;

            // Tagged-cell lookups are static unless a searched draggable itself matches the tags.
            case RequireTaggedCellRule rt:
                AddMatching(rt.cellTags, all, acc);
                return;

            // Count / uniqueness over the occupants of a region.
            case NPerRowRule r:
                AddMatching(r.tags, all, acc);
                return;
            case NPerColumnRule r:
                AddMatching(r.tags, all, acc);
                return;
            case NPerSectionRule r:
                AddMatching(r.tags, all, acc);
                return;
            case UniqueByTagKeyRule u:
                if (u.filterTags != null && u.filterTags.Count > 0) AddMatching(u.filterTags, all, acc);
                else AddAll(all, acc);
                return;
            case RegionSumRule:
                AddAll(all, acc); // sums arbitrary occupant tag values; see IsPruneSafe note
                return;

            // Relational rules: depend on wherever the entities they target end up.
            case DistanceToTagRule dt:
                AddMatching(dt.targetTags, all, acc);
                return;
            case PositionObjectRule po:
                AddMatching(po.targetTags, all, acc);
                return;
            case SameSectionAsTagRule ss:
                AddMatching(ss.targetTags, all, acc);
                return;
            case NumericTagCompareRule nt:
                AddMatching(nt.targetTags, all, acc); // reads on-grid status of targets, not just position
                return;
            case DirectionalOffsetRule dof:
                AddMatching(dof.targetTags, all, acc);
                return;
            case NeighborCountRule nc:
                AddMatching(nc.neighborTags, all, acc);
                return;
            case LineOfSightRule los:
                AddMatching(los.targetTags, all, acc);
                AddMatching(los.blockerTags, all, acc);
                return;
            case BetweenRule bt:
                AddMatching(bt.endpointATags, all, acc);
                AddMatching(bt.endpointBTags, all, acc);
                return;
            case MatchingCellTagRule mc:
                AddMatching(mc.referenceTags, all, acc);  // the reference (e.g. the victim) moves during the search
                AddMatching(mc.cellEntityTags, all, acc); // a searched draggable could itself be a tile's object
                return;

            // Combinators depend on whatever any child depends on.
            case AnyOfRule any:
                foreach (var o in any.options) CollectDependencies(o, all, acc);
                return;
            case AllOfRule allOf:
                foreach (var o in allOf.options) CollectDependencies(o, all, acc);
                return;
            case CountOfRule cnt:
                foreach (var o in cnt.options) CollectDependencies(o, all, acc);
                return;
            case NotRule not:
                CollectDependencies(not.inner, all, acc);
                return;
            case IfThenRule ift:
                CollectDependencies(ift.condition, all, acc);
                CollectDependencies(ift.consequence, all, acc);
                return;

            // Re-anchors 'inner' onto each tagged subject, so it depends on where the subjects are AND on
            // whatever 'inner' depends on (computed without excluding anyone — the subject stands in as self).
            case AsTaggedEntityRule ate:
                AddMatching(ate.subjectTags, all, acc);
                CollectDependencies(ate.inner, all, acc);
                return;

            default:
                // Unknown rule type added without updating this switch: depend on everyone so the
                // search fails safe (slower) instead of silently pruning away valid layouts.
                AddAll(all, acc);
                return;
        }
    }

    static void AddMatching(List<GridEntity.TagEntry> tags, List<Draggable> all, HashSet<Draggable> acc)
    {
        if (tags == null) return;
        foreach (var d in all)
            if (d.Entity != null && d.Entity.MatchesAll(tags))
                acc.Add(d);
    }

    static void AddAll(List<Draggable> all, HashSet<Draggable> acc)
    {
        foreach (var d in all) acc.Add(d);
    }
}
