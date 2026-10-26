using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Deduction-only companion to the brute-force verifier: replays the puzzle the way a player would,
/// never guessing, and records each forced step ("Adam → (5,7)", "Hannah cannot be the killer", ...).
///
/// Soundness rule: an elimination is made only if it holds in EVERY completion. A rule whose verdict
/// is not final yet (it depends on suspects that are still unplaced and is not monotone, see
/// <c>IsPruneSafe</c>) is never used directly; it only counts through Support (all positions its unplaced
/// dependencies could take are tried) or through a hypothesis that ends in a contradiction.
///
/// Tiers, cheapest first: Direct, Support, Contradiction, Chain (see <see cref="LogicTier"/>).
/// </summary>
public static partial class PuzzleUniquenessVerifier
{
    public enum LogicTier { Direct = 1, Support = 2, Contradiction = 3, Chain = 4 }

    public enum StepKind { Place, NotKiller, KillerForced }

    [System.Serializable]
    public struct SolveStep
    {
        public int index;        // 1-based
        public StepKind kind;
        public string suspect;
        public int row, col;     // Place only
        public LogicTier tier;
        public string reason;
    }

    sealed class LogicSolver
    {
        public sealed class Result
        {
            public List<SolveStep> steps = new();
            public string verdict;
            public string text;
            public int killerForcedAtStep;
            public bool fullyPlaced;
        }

        sealed class LState
        {
            public bool[][] live;   // [suspect][cell]
            public int[] cnt;       // live cells per suspect
            public int[] cell;      // placed cell, or -1

            public LState Clone() => new LState
            {
                live = live.Select(a => (bool[])a.Clone()).ToArray(),
                cnt = (int[])cnt.Clone(),
                cell = (int[])cell.Clone(),
            };
        }

        struct Elim
        {
            public int cell;
            public LogicTier tier;
            public string text;
            public bool listCell;   // tiers 1–2 group many cells under one text; hypotheses name their own cell
        }

        // A Support check enumerates every position of the rule's unplaced dependencies; beyond this many
        // combinations the rule is simply not used (sound: it just eliminates less).
        const double SupportCap = 2000;

        readonly GridManager gm;
        readonly Solver sv;
        readonly Draggable[] ds;
        readonly int n, rows, cols;
        readonly LogicTier maxTier;
        readonly int chainDepth;
        readonly double maxSeconds;

        readonly Rule[] killerRules;
        readonly bool[] killerSafe;
        readonly int[][][] killerDeps;   // [suspect][killer rule]

        // Board rules of the "at most K matching per region" family, used by the region-claim technique.
        struct ClaimRule
        {
            public string label;
            public RegionScope scope;
            public List<GridEntity.TagEntry> tags;
            public int capacity;
        }
        readonly List<ClaimRule> claimRules = new();
        bool[][] claimMatches;           // [claim rule][suspect]: suspect is constrained by that rule

        LState cur;
        LState boardOwner;               // state the live board currently mirrors
        readonly int[] onBoard;
        readonly int[] ubuf, chosen;
        string lastWhy;

        readonly List<Elim>[] pending;   // eliminations not yet explained by a step
        bool[][] everEliminated;         // static + deduced, for the cross-check against enumerated layouts
        readonly bool[] candidate;       // may still be the killer
        readonly int[] initialCount;

        readonly Result result = new();
        LogicTier lastKillerTier = LogicTier.Direct;
        bool inconsistent, timedOut;
        System.Diagnostics.Stopwatch clock;

        public LogicSolver(GridManager gm, Solver sv, Options options)
        {
            this.gm = gm;
            this.sv = sv;
            ds = sv.ds;
            n = sv.n;
            rows = sv.rows;
            cols = sv.cols;
            maxTier = options.maxTier;
            chainDepth = options.maxChainDepth;
            maxSeconds = options.maxSeconds;

            onBoard = Enumerable.Repeat(-1, n).ToArray();
            ubuf = new int[n];
            chosen = new int[n];
            pending = new List<Elim>[n];
            for (int i = 0; i < n; i++) pending[i] = new List<Elim>();
            candidate = Enumerable.Repeat(true, n).ToArray();
            initialCount = new int[n];

            foreach (var rule in gm.boardRules.Where(r => r != null))
            {
                var (scope, tags, mode, cnt) = rule switch
                {
                    NPerRowRule r     => (RegionScope.Row, r.tags, r.comparison, r.n),
                    NPerColumnRule r  => (RegionScope.Column, r.tags, r.comparison, r.n),
                    NPerSectionRule r => (RegionScope.Section, r.tags, r.comparison, r.n),
                    _                 => (RegionScope.Board, null, ComparisonMode.GreaterThan, 0),
                };
                if (tags == null) continue;
                // "others in the region == 0" and "others < K" both cap the region at K matching occupants.
                int capacity = mode == ComparisonMode.EqualTo && cnt == 0 ? 1
                    : mode == ComparisonMode.LessThan && cnt >= 1 ? cnt : 0;
                if (capacity == 0) continue;
                claimRules.Add(new ClaimRule { label = "board: " + rule.name, scope = scope, tags = tags, capacity = capacity });
            }
            claimMatches = claimRules.Select(c => ds.Select(d => d.Entity.MatchesAll(c.tags)).ToArray()).ToArray();

            var all = ds.ToList();
            killerRules = gm.killerRules.Where(r => r != null).ToArray();
            killerSafe = killerRules.Select(IsPruneSafe).ToArray();
            killerDeps = new int[n][][];
            for (int i = 0; i < n; i++)
            {
                killerDeps[i] = new int[killerRules.Length][];
                for (int k = 0; k < killerRules.Length; k++)
                {
                    var set = new HashSet<Draggable>();
                    CollectDependencies(killerRules[k], all, set);
                    set.Remove(ds[i]);
                    killerDeps[i][k] = set.Select(d => System.Array.IndexOf(ds, d)).OrderBy(x => x).ToArray();
                }
            }
        }

        // ── Entry point ───────────────────────────────────────────────────

        public Result Run(List<LayoutInfo> layouts)
        {
            clock = System.Diagnostics.Stopwatch.StartNew();

            int cells = rows * cols;
            cur = new LState { live = new bool[n][], cnt = new int[n], cell = Enumerable.Repeat(-1, n).ToArray() };
            everEliminated = new bool[n][];
            for (int i = 0; i < n; i++)
            {
                cur.live[i] = new bool[cells];
                everEliminated[i] = Enumerable.Repeat(true, cells).ToArray();
                foreach (int c in sv.initialDomains[i])
                {
                    cur.live[i][c] = true;
                    everEliminated[i][c] = false;
                }
                cur.cnt[i] = initialCount[i] = sv.initialDomains[i].Length;
            }

            try { Solve(); }
            finally { ClearBoard(); }

            return Finish(layouts);
        }

        void Solve()
        {
            while (!inconsistent && !Over())
            {
                if (Reach(cur, support: false, fixpoint: true, log: true, 0, out _) < 0) { inconsistent = true; break; }
                if (PlaceNextSingle()) continue;
                if (KillerSweep(LogicTier.Direct)) continue;
                if (AllPlaced()) break;

                if (maxTier >= LogicTier.Support)
                {
                    int r = Reach(cur, support: true, fixpoint: false, log: true, claimGroup: 1, out _);
                    if (r < 0) { inconsistent = true; break; }
                    if (r > 0) continue;
                    if (KillerSweep(LogicTier.Support)) continue;
                }
                if (maxTier >= LogicTier.Contradiction)
                {
                    // Easiest first: "if X stood here some suspect would have no cell" using only decided rules,
                    // then pigeonhole over several suspects, then contradictions that need Support / claims.
                    if (ContradictionPass(0, LogicTier.Contradiction, simple: true)) continue;
                    if (GroupClaimPass()) continue;
                    if (ContradictionPass(0, LogicTier.Contradiction)) continue;
                    if (KillerSweep(LogicTier.Contradiction)) continue;
                }
                if (maxTier >= LogicTier.Chain)
                {
                    if (ContradictionPass(chainDepth, LogicTier.Chain)) continue;
                    if (KillerSweep(LogicTier.Chain)) continue;
                }
                break; // stuck
            }
        }

        bool Over()
        {
            if (!timedOut && clock.Elapsed.TotalSeconds > maxSeconds) timedOut = true;
            return timedOut;
        }

        bool AllPlaced() => cur.cell.All(c => c >= 0);

        // ── Board mirroring ───────────────────────────────────────────────

        void Sync(LState s)
        {
            if (boardOwner == s) return;
            ClearBoard();
            for (int i = 0; i < n; i++)
            {
                int c = s.cell[i];
                if (c < 0) continue;
                int r = c / cols, cc = c % cols;
                if (!gm.TryPlace(ds[i], r, cc)) continue;
                ds[i].Entity.Row = r;
                ds[i].Entity.Col = cc;
                onBoard[i] = c;
            }
            boardOwner = s;
        }

        void ClearBoard()
        {
            for (int i = 0; i < n; i++)
            {
                if (onBoard[i] >= 0) gm.Release(ds[i], onBoard[i] / cols, onBoard[i] % cols);
                onBoard[i] = -1;
                ds[i].Entity.Row = -1;
                ds[i].Entity.Col = -1;
            }
            boardOwner = null;
        }

        void Place(LState s, int i, int cell)
        {
            System.Array.Clear(s.live[i], 0, s.live[i].Length);
            s.live[i][cell] = true;
            s.cnt[i] = 1;
            s.cell[i] = cell;
            boardOwner = null;
        }

        string CellText(int cell) => $"({cell / cols},{cell % cols})";

        // ── Rule probing ──────────────────────────────────────────────────

        /// <summary>
        /// False = 'rule' provably fails for 'owner' standing on 'cell' in every completion of state s.
        /// The owner's entity position is set here; callers restore it.
        /// </summary>
        bool RuleHolds(LState s, int owner, Rule rule, bool safe, int[] depIdx, int cell, bool support)
        {
            lastWhy = null;
            var d = ds[owner];
            int r = cell / cols, c = cell % cols;
            d.Entity.Row = r;
            d.Entity.Col = c;

            int uc = 0;
            double product = 1;
            for (int k = 0; k < depIdx.Length; k++)
            {
                int x = depIdx[k];
                if (s.cell[x] >= 0) continue;
                ubuf[uc++] = x;
                product *= s.cnt[x];
            }

            if (uc == 0) return rule.CanPlace(gm, d, r, c);                 // verdict is final
            if (safe && !rule.CanPlace(gm, d, r, c)) return false;          // monotone: false now ⇒ false forever
            if (!support || product > SupportCap) return true;              // cannot decide: do not eliminate

            bool any = Enumerate(s, rule, d, r, c, cell, uc, 0);
            if (!any) lastWhy = string.Join(", ", Enumerable.Range(0, uc).Select(k => ds[ubuf[k]].name));
            return any;
        }

        bool Enumerate(LState s, Rule rule, Draggable d, int r, int c, int ownerCell, int uc, int idx)
        {
            if (idx == uc) return rule.CanPlace(gm, d, r, c);

            int x = ubuf[idx];
            var dx = ds[x];
            var lv = s.live[x];
            for (int cell = 0; cell < lv.Length; cell++)
            {
                if (!lv[cell] || cell == ownerCell) continue;
                bool clash = false;
                for (int m = 0; m < idx; m++) if (chosen[m] == cell) { clash = true; break; }
                if (clash) continue;

                int r2 = cell / cols, c2 = cell % cols;
                if (!gm.TryPlace(dx, r2, c2)) continue;
                dx.Entity.Row = r2;
                dx.Entity.Col = c2;
                chosen[idx] = cell;

                bool ok = Enumerate(s, rule, d, r, c, ownerCell, uc, idx + 1);

                gm.Release(dx, r2, c2);
                dx.Entity.Row = -1;
                dx.Entity.Col = -1;
                if (ok) return true;
            }
            return false;
        }

        string RuleLabel(int owner, int j) =>
            j < sv.ownCount[owner] ? sv.rules[owner][j].name : "board: " + sv.rules[owner][j].name;

        void Remove(LState s, int i, int cell, LogicTier tier, string text, bool listCell, bool log)
        {
            s.live[i][cell] = false;
            s.cnt[i]--;
            if (!log) return;
            everEliminated[i][cell] = true;
            pending[i].Add(new Elim { cell = cell, tier = tier, text = text, listCell = listCell });
        }

        // ── Propagation ───────────────────────────────────────────────────

        /// <summary>
        /// Applies cell exclusivity and every rule that is decided in state s. Returns -1 on contradiction
        /// ('why' says who ran dry), else the number of cells removed. With 'support' the rules whose
        /// dependencies are unplaced are also checked against every position those could take.
        /// Without a fixpoint it does a single pass (the main line wants tiers kept apart).
        /// </summary>
        int Reach(LState s, bool support, bool fixpoint, bool log, int claimGroup, out string why)
        {
            why = null;
            int total = 0;
            var tier = support ? LogicTier.Support : LogicTier.Direct;
            Sync(s);

            bool changed;
            do
            {
                changed = false;
                if (Over()) return total;

                // Cell exclusivity.
                for (int i = 0; i < n; i++)
                {
                    int ci = s.cell[i];
                    if (ci < 0) continue;
                    for (int k = 0; k < n; k++)
                    {
                        if (s.cell[k] >= 0 || !s.live[k][ci]) continue;
                        Remove(s, k, ci, LogicTier.Direct, "cell taken by " + ds[i].name, true, log);
                        total++;
                        changed = true;
                        if (s.cnt[k] == 0) { why = $"{ds[k].name} has no cell left (cell taken)"; return -1; }
                    }
                }

                // Region claims: a suspect whose every remaining cell lies in one region has spent that
                // region's capacity, even though its exact cell is still unknown.
                if (support)
                {
                    int cr = RegionClaims(s, log, claimGroup, out why);
                    if (cr < 0) return -1;
                    if (cr > 0)
                    {
                        total += cr;
                        changed = true;
                        // Main line: hand back so the cheap tiers digest the new eliminations (and place any suspect
                        // left alone) before more is deduced from them.
                        if (!fixpoint) return total;
                    }
                }

                // Bidirectional check: rules of the suspects that are already placed.
                for (int i = 0; i < n; i++)
                {
                    if (s.cell[i] < 0) continue;
                    var rs = sv.rules[i];
                    for (int j = 0; j < rs.Length; j++)
                    {
                        if (!RuleHolds(s, i, rs[j], sv.safe[i][j], sv.deps[i][j], s.cell[i], support))
                        {
                            why = $"{ds[i].name} would break {RuleLabel(i, j)}";
                            return -1;
                        }
                    }
                }

                // Domain filtering of the unplaced suspects.
                for (int i = 0; i < n; i++)
                {
                    if (s.cell[i] >= 0) continue;
                    var rs = sv.rules[i];
                    string lastRule = null;
                    for (int cell = 0; cell < s.live[i].Length; cell++)
                    {
                        if (!s.live[i][cell]) continue;
                        for (int j = 0; j < rs.Length; j++)
                        {
                            if (RuleHolds(s, i, rs[j], sv.safe[i][j], sv.deps[i][j], cell, support)) continue;
                            lastRule = RuleLabel(i, j);
                            string text = lastWhy != null ? $"{lastRule}, whichever cell {lastWhy} takes" : lastRule;
                            Remove(s, i, cell, lastWhy != null ? LogicTier.Support : tier, text, true, log);
                            total++;
                            changed = true;
                            break;
                        }
                    }
                    ds[i].Entity.Row = -1;
                    ds[i].Entity.Col = -1;
                    if (s.cnt[i] == 0) { why = $"{ds[i].name} has no cell left ({lastRule})"; return -1; }
                    if (Over()) return total;
                }
            } while (changed && fixpoint);

            return total;
        }

        int RegionOf(RegionScope scope, int cell) => scope switch
        {
            RegionScope.Row     => cell / cols,
            RegionScope.Column  => cell % cols,
            RegionScope.Section => gm.GetSection(cell / cols, cell % cols),
            _                   => -1,
        };

        // Largest group of suspects the region-claim search considers (C(n,≤4) subsets per rule).
        const int ClaimMaxGroup = 4;

        static int PopCount(ulong v)
        {
            int c = 0;
            for (; v != 0; v &= v - 1) c++;
            return c;
        }

        /// <summary>
        /// For every "at most K matching per row/column/section" board rule, applies the pigeonhole principle:
        /// if a group of matching suspects has every live cell inside a set R of regions, then
        ///   • more than K·|R| of them is a contradiction, and
        ///   • exactly K·|R| of them fills R completely, so nobody else matching the rule may use R.
        /// One suspect confined to one row is the smallest case; two suspects confined to the same two rows the next.
        /// Returns -1 on contradiction, else the number of cells removed.
        /// </summary>
        int RegionClaims(LState s, bool log, int maxGroup, out string why)
        {
            string fail = null;
            int total = 0;
            // Stop at the first batch of removals: every later group was computed from domains that the batch
            // has just made stale, and the simpler consequences (a suspect now alone on a cell) should surface first.
            bool done = false;
            var masks = new ulong[n];
            var group = new int[ClaimMaxGroup];
            var cand = new List<int>();

            for (int ri = 0; ri < claimRules.Count && fail == null && !done; ri++)
            {
                var cr = claimRules[ri];
                var matches = claimMatches[ri];
                string regionName = cr.scope.ToString().ToLowerInvariant();

                // Candidates: matching suspects whose live cells all lie in regions we can name (bit per region id).
                cand.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (!matches[i]) continue;
                    ulong m = 0;
                    bool ok = true;
                    for (int cell = 0; cell < s.live[i].Length && ok; cell++)
                    {
                        if (!s.live[i][cell]) continue;
                        int r = RegionOf(cr.scope, cell);
                        if (r < 0 || r >= 64) ok = false;
                        else m |= 1UL << r;
                    }
                    if (ok && m != 0) { masks[i] = m; cand.Add(i); }
                }
                if (cand.Count == 0) continue;

                // Masks go stale as cells are removed during this pass; a stale mask is a superset of the true
                // one, which only makes a group look less confined — sound, and the caller's fixpoint redoes it.
                // Group sizes are tried smallest first so every elimination is credited to the simplest argument
                // ("Daphne is alone in row 2") rather than to a larger group that happens to imply it.
                int target = 1;
                void Recurse(int start, int size, ulong mask)
                {
                    if (fail != null || done) return;
                    if (size == target)
                    {
                        int regions = PopCount(mask);
                        if (size > cr.capacity * regions)
                        {
                            fail = $"{GroupNames(group, size)} would all be confined to {RegionList(regionName, mask)} ({cr.label})";
                            return;
                        }
                        if (size == cr.capacity * regions)
                            total += ApplyClaim(s, cr, matches, group, size, mask, regionName, log, ref fail);
                        done = total > 0;
                        return;
                    }
                    for (int k = start; k < cand.Count; k++)
                    {
                        ulong grown = mask | masks[cand[k]];
                        if (PopCount(grown) * cr.capacity > target) continue;   // too many regions for a group this small
                        group[size] = cand[k];
                        Recurse(k + 1, size + 1, grown);
                        if (fail != null || done) return;
                    }
                }
                for (target = 1; target <= maxGroup && fail == null && !done; target++)
                    Recurse(0, 0, 0UL);
            }

            why = fail;
            return fail != null ? -1 : total;
        }

        string GroupNames(int[] group, int size) =>
            string.Join(" and ", Enumerable.Range(0, size).Select(k => ds[group[k]].name));

        static string RegionList(string regionName, ulong mask)
        {
            var ids = Enumerable.Range(0, 64).Where(b => (mask & (1UL << b)) != 0).ToList();
            return ids.Count == 1 ? $"{regionName} {ids[0]}" : $"{regionName}s {string.Join(", ", ids)}";
        }

        /// <summary>Removes the claimed regions' cells from every matching suspect outside the group.</summary>
        int ApplyClaim(LState s, ClaimRule cr, bool[] matches, int[] group, int size, ulong mask,
            string regionName, bool log, ref string fail)
        {
            int removed = 0;
            for (int y = 0; y < n; y++)
            {
                if (!matches[y] || s.cell[y] >= 0 || System.Array.IndexOf(group, y, 0, size) >= 0) continue;
                for (int cell = 0; cell < s.live[y].Length; cell++)
                {
                    if (!s.live[y][cell]) continue;
                    int r = RegionOf(cr.scope, cell);
                    if (r < 0 || r >= 64 || (mask & (1UL << r)) == 0) continue;
                    string claimed = RegionList(regionName, mask);
                    Remove(s, y, cell, size > 1 ? LogicTier.Contradiction : LogicTier.Support,
                        $"{claimed} {(PopCount(mask) == 1 ? "is" : "are")} already claimed by {GroupNames(group, size)}, whose cells are all there ({cr.label})",
                        true, log);
                    removed++;
                }
                if (s.cnt[y] == 0)
                {
                    fail = $"{ds[y].name} has no cell left ({RegionList(regionName, mask)} claimed by {GroupNames(group, size)})";
                    return removed;
                }
            }
            return removed;
        }

        // ── Hypotheses (Contradiction / Chain) ────────────────────────────

        /// <summary>Null = no contradiction found. Otherwise the text explaining why 'i' cannot stand on 'cell'.</summary>
        string Hypothesis(int i, int cell, int depth, bool simple = false)
        {
            var h = cur.Clone();
            Place(h, i, cell);
            string chain = $"{ds[i].name}@{CellText(cell)}";

            for (int step = 0; ; step++)
            {
                if (Reach(h, support: !simple, fixpoint: true, log: false, ClaimMaxGroup, out string why) < 0)
                    return $"{chain} ⇒ {why}";
                if (step >= depth || Over()) return null;

                int single = -1;
                for (int k = 0; k < n; k++)
                    if (h.cell[k] < 0 && h.cnt[k] == 1) { single = k; break; }
                if (single < 0) return null;

                int sc = System.Array.IndexOf(h.live[single], true);
                Place(h, single, sc);
                chain += $" ⇒ {ds[single].name}@{CellText(sc)}";
            }
        }

        /// <summary>Region claims by groups of two or more suspects (the single-suspect ones already ran in the Support pass).</summary>
        bool GroupClaimPass()
        {
            Sync(cur);
            int r = RegionClaims(cur, true, ClaimMaxGroup, out _);
            if (r < 0) inconsistent = true;
            return r > 0;
        }

        bool ContradictionPass(int depth, LogicTier tier, bool simple = false)
        {
            bool any = false;
            var order = Enumerable.Range(0, n).Where(i => cur.cell[i] < 0 && cur.cnt[i] > 1)
                .OrderBy(i => cur.cnt[i]).ThenBy(i => i).ToList();
            foreach (int i in order)
            {
                for (int cell = 0; cell < cur.live[i].Length; cell++)
                {
                    if (!cur.live[i][cell] || cur.cnt[i] <= 1) continue;
                    if (Over()) return any;
                    string why = Hypothesis(i, cell, depth, simple);
                    if (why == null) continue;
                    Remove(cur, i, cell, tier, why, false, true);
                    any = true;
                }
                if (any) return true;   // restart from the cheap tiers as soon as something changed
            }
            return any;
        }

        // ── Steps ─────────────────────────────────────────────────────────

        bool PlaceNextSingle()
        {
            for (int i = 0; i < n; i++)
            {
                if (cur.cell[i] >= 0 || cur.cnt[i] != 1) continue;
                int cell = System.Array.IndexOf(cur.live[i], true);
                Place(cur, i, cell);
                var (tier, reason) = Explain(i);
                AddStep(StepKind.Place, i, cell, tier, reason);
                return true;
            }
            return false;
        }

        (LogicTier tier, string reason) Explain(int i)
        {
            var list = pending[i];
            pending[i] = new List<Elim>();
            if (list.Count == 0)
                return (LogicTier.Direct, initialCount[i] == 1 ? "the only cell its static clues leave" : "");

            var tier = list.Max(e => e.tier);
            var groups = list
                .GroupBy(e => (e.tier, e.text, e.listCell))
                .OrderBy(g => g.Key.tier).ThenByDescending(g => g.Count()).ToList();
            var parts = new List<string>();
            foreach (var g in groups.Take(6))
            {
                if (!g.Key.listCell) { parts.Add(g.Key.text); continue; }
                var cells = g.Select(e => e.cell).OrderBy(c => c).ToList();
                parts.Add(cells.Count <= 4
                    ? $"{g.Key.text}: {string.Join(" ", cells.Select(CellText))}"
                    : $"{g.Key.text}: {cells.Count} cells");
            }
            if (groups.Count > 6) parts.Add($"+{groups.Count - 6} more");
            return (tier, string.Join("; ", parts));
        }

        void AddStep(StepKind kind, int i, int cell, LogicTier tier, string reason)
        {
            result.steps.Add(new SolveStep
            {
                index = result.steps.Count + 1,
                kind = kind,
                suspect = ds[i].name,
                row = cell >= 0 ? cell / cols : -1,
                col = cell >= 0 ? cell % cols : -1,
                tier = tier,
                reason = reason,
            });
        }

        // ── Killer deduction ──────────────────────────────────────────────

        bool KillerSweep(LogicTier tier)
        {
            if (killerRules.Length == 0) return false;
            bool any = false;

            for (int x = 0; x < n && !Over(); x++)
            {
                if (!candidate[x]) continue;

                bool viable = false;
                int cellsSeen = 0;
                var fails = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
                for (int cell = 0; cell < cur.live[x].Length && !viable; cell++)
                {
                    if (!cur.live[x][cell]) continue;
                    cellsSeen++;

                    Sync(cur);
                    string failed = null;
                    for (int k = 0; k < killerRules.Length; k++)
                    {
                        if (RuleHolds(cur, x, killerRules[k], killerSafe[k], killerDeps[x][k], cell, tier >= LogicTier.Support))
                            continue;
                        failed = killerRules[k].name;
                        break;
                    }
                    ds[x].Entity.Row = cur.cell[x] >= 0 ? cur.cell[x] / cols : -1;
                    ds[x].Entity.Col = cur.cell[x] >= 0 ? cur.cell[x] % cols : -1;

                    if (failed == null && tier >= LogicTier.Contradiction)
                    {
                        if (Hypothesis(x, cell, tier == LogicTier.Chain ? chainDepth : 0) != null)
                            failed = "(cell is contradictory)";
                        Sync(cur);
                    }

                    if (failed == null) viable = true;
                    else fails[failed] = fails.TryGetValue(failed, out int v) ? v + 1 : 1;
                }

                if (viable) continue;
                candidate[x] = false;
                lastKillerTier = tier;
                AddStep(StepKind.NotKiller, x, -1, tier,
                    $"none of its {cellsSeen} remaining cell(s) can satisfy the killer rules — "
                    + string.Join(", ", fails.Select(kv => $"{kv.Key} ×{kv.Value}")));
                any = true;
            }

            if (candidate.Count(c => c) == 1 && result.killerForcedAtStep == 0)
            {
                int g = System.Array.IndexOf(candidate, true);
                AddStep(StepKind.KillerForced, g, -1, lastKillerTier, "every other suspect was ruled out as killer");
                result.killerForcedAtStep = result.steps.Count;
                any = true;
            }
            return any;
        }

        // ── Report ────────────────────────────────────────────────────────

        Result Finish(List<LayoutInfo> layouts)
        {
            result.fullyPlaced = AllPlaced();
            var sb = new StringBuilder();
            sb.AppendLine($"Logical solution (deduction only, max tier: {maxTier}"
                + (maxTier == LogicTier.Chain ? $", chain depth {chainDepth}" : "") + "):");
            foreach (var s in result.steps)
            {
                string what = s.kind switch
                {
                    StepKind.Place => $"{s.suspect} → ({s.row},{s.col})",
                    StepKind.NotKiller => $"{s.suspect} is NOT the killer",
                    _ => $"KILLER = {s.suspect}",
                };
                sb.AppendLine($"  {s.index,2}. [{s.tier}] {what}" + (string.IsNullOrEmpty(s.reason) ? "" : " — " + s.reason));
            }

            var bugs = Crosscheck(layouts);

            var unplaced = Enumerable.Range(0, n).Where(i => cur.cell[i] < 0).ToList();
            string stuck = unplaced.Count == 0 ? "" : " Still open: " + string.Join("; ", unplaced.Select(i =>
            {
                var cells = Enumerable.Range(0, cur.live[i].Length).Where(c => cur.live[i][c]).ToList();
                return $"{ds[i].name} {{{string.Join(" ", cells.Take(6).Select(CellText))}{(cells.Count > 6 ? " …" : "")}}}";
            })) + ".";
            string killerPart = result.killerForcedAtStep > 0
                ? $"killer forced at step {result.killerForcedAtStep} ({result.steps[result.killerForcedAtStep - 1].suspect})"
                : $"killer NOT forced (still possible: {string.Join(", ", Enumerable.Range(0, n).Where(i => candidate[i]).Select(i => ds[i].name))})";

            string verdict;
            if (inconsistent) verdict = "LOGIC SOLVER BUG: a contradiction was derived although layouts exist.";
            else if (result.fullyPlaced) verdict = $"LOGIC: fully solvable by deduction in {result.steps.Count} steps; {killerPart}.";
            else verdict = $"LOGIC: placement STUCK at tier {maxTier}; {killerPart}.{stuck}";
            if (timedOut) verdict += $" (logic time budget of {maxSeconds:0.#}s hit — result is partial)";
            if (bugs.Count > 0) verdict = "LOGIC SOLVER BUG: " + string.Join(" | ", bugs.Take(3)) + " — " + verdict;

            sb.AppendLine(verdict);
            result.verdict = verdict;
            result.text = sb.ToString();
            return result;
        }

        /// <summary>Every deduction must be consistent with every layout the exhaustive search found.</summary>
        List<string> Crosscheck(List<LayoutInfo> layouts)
        {
            var bugs = new List<string>();
            foreach (var layout in layouts)
            {
                foreach (var p in layout.placements)
                {
                    int i = System.Array.FindIndex(ds, d => d.name == p.suspect);
                    if (i >= 0 && everEliminated[i][p.row * cols + p.col])
                        bugs.Add($"{p.suspect}@({p.row},{p.col}) was eliminated but a valid layout uses it");
                }
                foreach (var k in layout.killers)
                {
                    int i = System.Array.FindIndex(ds, d => d.name == k);
                    if (i >= 0 && !candidate[i])
                        bugs.Add($"{k} was ruled out as killer but is the killer of a valid layout");
                }
            }
            return bugs;
        }
    }
}
