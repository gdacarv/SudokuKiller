using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Front end for <see cref="PuzzleUniquenessVerifier"/>: options, the verdict, the enumerated layouts
/// (with Apply) and the step-by-step logical solution. Lives in its own window so Level Rules can stay
/// a pure rule-authoring surface.
/// </summary>
public class PuzzleVerifierWindow : EditorWindow
{
    // Serialized so the last result survives domain reloads (play mode, recompiles).
    [SerializeField] private PuzzleUniquenessVerifier.VerificationResult _verifyResult;
    [SerializeField] private bool   _hasVerifyResult;
    [SerializeField] private bool   _verifyDetails;
    [SerializeField] private bool   _verifySteps;
    [SerializeField] private bool   _verifyStale;
    [SerializeField] private string _verifyScenePath;
    [SerializeField] private string _verifyScriptStamp;
    private Vector2 _scroll;
    private bool    _suppressChangeEvents;

    // Verifier limits the user can change before running. Persisted by the verifier itself
    // (EditorPrefs) rather than serialized here, so the Tools menu item shares the same values.
    [SerializeField] private bool _verifyOptionsOpen;
    [System.NonSerialized] private PuzzleUniquenessVerifier.Options _verifyOptions = PuzzleUniquenessVerifier.Options.Default;
    [System.NonSerialized] private bool _verifyQueued;

    private static readonly Color ColorSeparator = new(0.50f, 0.50f, 0.50f, 0.35f);

    // Last-write stamp of the compiled script assemblies. Changes only when code
    // recompiles, so play-mode domain reloads keep a result fresh while script
    // edits (which can change rule semantics) mark it stale.
    private static string CurrentScriptStamp()
    {
        long max = 0;
        var dir = "Library/ScriptAssemblies";
        if (System.IO.Directory.Exists(dir))
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.dll"))
            {
                long t = System.IO.File.GetLastWriteTimeUtc(f).Ticks;
                if (t > max) max = t;
            }
        return max.ToString();
    }

    [MenuItem("Tools/Sudoku Killers/Puzzle Verifier Window")]
    public static void Open() => GetWindow<PuzzleVerifierWindow>("Puzzle Verifier");

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        titleContent = new GUIContent("Puzzle Verifier", EditorGUIUtility.FindTexture("d_Search Icon"));
        EditorSceneManager.sceneOpened                  += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        ObjectChangeEvents.changesPublished             += OnObjectsChanged;
        Undo.undoRedoPerformed                          += OnUndoRedo;

        _verifyOptions = PuzzleUniquenessVerifier.Options.Load();

        // Coming back from a domain reload: drop a result that belongs to another
        // scene; mark it stale if scripts recompiled since it was computed.
        ClearVerifyResultIfSceneChanged();
        if (_hasVerifyResult && _verifyScriptStamp != CurrentScriptStamp())
            _verifyStale = true;
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened                  -= OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        ObjectChangeEvents.changesPublished             -= OnObjectsChanged;
        Undo.undoRedoPerformed                          -= OnUndoRedo;
    }

    // Undo/redo can move suspects (e.g. undoing an applied layout) — keep the
    // "(authored solution)" tag on layout rows truthful.
    private void OnUndoRedo()
    {
        if (!_hasVerifyResult || _verifyResult.layouts == null) return;

        var solutions = new Dictionary<string, SolutionPosition>();
        foreach (var d in FindObjectsByType<Draggable>(FindObjectsSortMode.None))
        {
            var sp = d.GetComponent<SolutionPosition>();
            if (sp != null) solutions[d.name] = sp;
        }

        foreach (var layout in _verifyResult.layouts)
            layout.matchesAuthored = layout.placements.All(p =>
                solutions.TryGetValue(p.suspect, out var sp)
                && sp.solutionRow == p.row && sp.solutionCol == p.col);

        Repaint();
    }

    private void OnSceneOpened(Scene scene, OpenSceneMode mode) { ClearVerifyResultIfSceneChanged(); Repaint(); }
    private void OnActiveSceneChanged(Scene prev, Scene next)   { ClearVerifyResultIfSceneChanged(); Repaint(); }

    // Exiting play mode re-fires scene-change events for the same scene —
    // only drop the result when the active scene really is a different one.
    private void ClearVerifyResultIfSceneChanged()
    {
        if (_hasVerifyResult && _verifyScenePath != SceneManager.GetActiveScene().path)
            ClearVerifyResult();
    }

    private void ClearVerifyResult()
    {
        _hasVerifyResult   = false;
        _verifyStale       = false;
        _verifyScenePath   = null;
        _verifyScriptStamp = null;
    }

    // Any scene-object or asset modification (inspector edits, moves, undo,
    // prefab updates, rule-asset changes…) invalidates the last verification.
    private void OnObjectsChanged(ref ObjectChangeEventStream stream)
    {
        if (_suppressChangeEvents || Application.isPlaying) return;
        if (!_hasVerifyResult || _verifyStale) return;
        _verifyStale = true;
        Repaint();
    }

    // ── GUI ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        GUILayout.Label("Puzzle Verifier", EditorStyles.boldLabel);

        DrawVerifyOptions();

        using (new EditorGUI.DisabledScope(Application.isPlaying || _verifyQueued))
        {
            if (GUILayout.Button(_verifyQueued ? "Verifying…" : "Verify Puzzle Uniqueness", GUILayout.Height(24)))
            {
                // Never run the search inside OnGUI: it blocks for as long as the search takes, in the
                // middle of a repaint, with layout/control state half-built. Defer to the editor's
                // idle loop so the click returns immediately.
                _verifyQueued = true;
                EditorApplication.delayCall += RunVerification;
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Verification runs in Edit Mode only.", MessageType.None);

        if (!_hasVerifyResult)
        {
            EditorGUILayout.HelpBox("No result yet for this scene.", MessageType.None);
            return;
        }
        var result = _verifyResult;

        if (_verifyStale)
            EditorGUILayout.HelpBox("Stale — the scene or assets changed since this result. Re-verify.", MessageType.Warning);

        var messageType = result.outcome switch
        {
            PuzzleUniquenessVerifier.Outcome.Unique       => MessageType.Info,
            PuzzleUniquenessVerifier.Outcome.KillerUnique => MessageType.Warning,
            PuzzleUniquenessVerifier.Outcome.Inconclusive => MessageType.Warning,
            PuzzleUniquenessVerifier.Outcome.Cancelled    => MessageType.Warning,
            _                                             => MessageType.Error,
        };
        using (new EditorGUI.DisabledScope(_verifyStale))
            EditorGUILayout.HelpBox(result.verdict, messageType);

        DrawSeparator();

        // One scroll view for everything below the verdict: with the window to itself there is no
        // need for the fixed-height details pane the Level Rules footer used.
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawLogicSolution(result);

        _verifyDetails = EditorGUILayout.Foldout(_verifyDetails, "Details", true);
        if (_verifyDetails)
        {
            string summary = result.summary ?? result.report;
            EditorGUILayout.SelectableLabel(summary,
                EditorStyles.miniLabel,
                GUILayout.MinHeight(EditorStyles.miniLabel.CalcHeight(new GUIContent(summary), position.width - 30)));

            if (result.layouts != null)
                for (int i = 0; i < result.layouts.Count; i++)
                    DrawLayoutRow(i, result.layouts[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawLogicSolution(PuzzleUniquenessVerifier.VerificationResult result)
    {
        if (string.IsNullOrEmpty(result.logicVerdict)) return;

        var type = result.logicVerdict.StartsWith("LOGIC SOLVER BUG") ? MessageType.Error
            : result.fullyPlaced ? MessageType.Info
            : result.killerForcedAtStep > 0 ? MessageType.Warning
            : MessageType.Error;
        using (new EditorGUI.DisabledScope(_verifyStale))
            EditorGUILayout.HelpBox(result.logicVerdict, type);

        _verifySteps = EditorGUILayout.Foldout(_verifySteps, $"Logical solution ({result.steps?.Count ?? 0} steps)", true);
        if (!_verifySteps) return;

        if (GUILayout.Button("Copy steps", EditorStyles.miniButton, GUILayout.Width(80)))
            EditorGUIUtility.systemCopyBuffer = result.logicReport;
        string text = BuildRichSteps(result);
        var style = WrappedMiniLabel;
        // Reserve room for the scrollbars so the wrapped height we measure matches what is drawn.
        float height = style.CalcHeight(new GUIContent(text), Mathf.Max(100f, position.width - 50f));
        _stepsScroll = EditorGUILayout.BeginScrollView(_stepsScroll, GUILayout.MaxHeight(StepsMaxHeight));
        EditorGUILayout.SelectableLabel(text, style, GUILayout.MinHeight(height));
        EditorGUILayout.EndScrollView();
    }

    private const float StepsMaxHeight = 320f;
    private Vector2 _stepsScroll;

    private static GUIStyle _wrappedMiniLabel;
    private static GUIStyle WrappedMiniLabel => _wrappedMiniLabel ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };

    // The window shows the steps with the headline of each one (who goes where) in bold; the plain
    // logicReport stays untouched for Copy steps and the Temp report files.
    private static string BuildRichSteps(PuzzleUniquenessVerifier.VerificationResult result)
    {
        string report = result.logicReport ?? "";
        if (result.steps == null || result.steps.Count == 0) return report;

        var sb = new System.Text.StringBuilder();
        int firstLineEnd = report.IndexOf('\n');
        sb.AppendLine(firstLineEnd >= 0 ? report.Substring(0, firstLineEnd).TrimEnd('\r') : report);
        foreach (var step in result.steps)
        {
            string what = step.kind switch
            {
                PuzzleUniquenessVerifier.StepKind.Place     => $"{step.suspect} → ({step.row},{step.col})",
                PuzzleUniquenessVerifier.StepKind.NotKiller => $"{step.suspect} is NOT the killer",
                _                                           => $"KILLER = {step.suspect}",
            };
            sb.Append($"{step.index,2}. [{step.tier}] <b>{what}</b>");
            if (!string.IsNullOrEmpty(step.reason)) sb.Append(" — ").Append(step.reason);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private void DrawVerifyOptions()
    {
        _verifyOptionsOpen = EditorGUILayout.Foldout(_verifyOptionsOpen,
            _verifyOptions.IsDefault ? "Options" : "Options (customized)", true);
        if (!_verifyOptionsOpen) return;

        using (new EditorGUI.DisabledScope(_verifyQueued))
        {
            // Delayed fields commit on Enter/blur, not per keystroke — otherwise clamping to the minimum
            // would fight the user mid-typing (typing "5000000" would snap to the floor at the first "5").
            EditorGUI.BeginChangeCheck();
            var o = _verifyOptions;
            EditorGUI.indentLevel++;
            o.maxSeconds = EditorGUILayout.DelayedDoubleField(new GUIContent("Time budget (s)",
                $"Wall-clock limit. When exceeded the run stops as INCONCLUSIVE. Default {PuzzleUniquenessVerifier.DefaultMaxSeconds:0.#}, minimum {PuzzleUniquenessVerifier.Options.MinSeconds:0.#}."),
                o.maxSeconds);
            double visits = EditorGUILayout.DelayedDoubleField(new GUIContent("Node budget",
                $"Placement attempts the search may make before stopping as INCONCLUSIVE. Default {PuzzleUniquenessVerifier.DefaultMaxVisits:N0}, minimum {PuzzleUniquenessVerifier.Options.MinVisits:N0}."),
                o.maxVisits);
            o.maxVisits = visits >= long.MaxValue ? long.MaxValue : (long)visits;
            o.maxRecordedLayouts = EditorGUILayout.DelayedIntField(new GUIContent("Layouts listed",
                $"How many valid layouts are kept and listed below. Counting and killer analysis always cover EVERY layout. Default {PuzzleUniquenessVerifier.DefaultMaxRecordedLayouts}, maximum {PuzzleUniquenessVerifier.Options.MaxRecordedCap} (each listed layout is drawn every repaint)."),
                o.maxRecordedLayouts);
            o.maxRecordedPerKillerSet = EditorGUILayout.DelayedIntField(new GUIContent("Listed per killer set",
                $"Cap on listed layouts sharing the same killer(s), so the list shows variety instead of one killer's layouts. Default {PuzzleUniquenessVerifier.DefaultMaxRecordedPerKillerSet}, maximum {PuzzleUniquenessVerifier.Options.MaxRecordedCap}."),
                o.maxRecordedPerKillerSet);
            o.stopWhenNotUnique = EditorGUILayout.Toggle(new GUIContent("Stop once NOT UNIQUE is proven",
                "Faster on under-constrained puzzles: stops as soon as 2+ layouts exist AND the killer rules already fail (none, several, or differing killers) — the final verdict can't change after that. " +
                "UNIQUE, KILLER-UNIQUE and BROKEN still need the full search. Trade-off: layout counts become 'at least N' and fewer layouts are listed. Default off."),
                o.stopWhenNotUnique);
            o.maxTier = (PuzzleUniquenessVerifier.LogicTier)EditorGUILayout.EnumPopup(new GUIContent("Max deduction tier",
                "Hardest technique the logical-solution pass may use. Direct: rules already decided. Support: also try every position a rule's unplaced dependencies could take. " +
                "Contradiction: 'if A stood here, someone would have no cell'. Chain: contradictions that follow forced placements. Default Contradiction."),
                o.maxTier);
            using (new EditorGUI.DisabledScope(o.maxTier != PuzzleUniquenessVerifier.LogicTier.Chain))
                o.maxChainDepth = EditorGUILayout.DelayedIntField(new GUIContent("Chain depth",
                    $"How many forced placements a Chain hypothesis may follow. Default 3, range {PuzzleUniquenessVerifier.Options.MinChainDepth}–{PuzzleUniquenessVerifier.Options.MaxChainDepthCap}."),
                    o.maxChainDepth);
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
            {
                _verifyOptions = o.Sanitized(); // show the clamped value back to the user
                _verifyOptions.Save();
                GUI.FocusControl(null);
            }

            using (new EditorGUI.DisabledScope(_verifyOptions.IsDefault))
            {
                if (GUILayout.Button("Reset to defaults", GUILayout.Width(120)))
                {
                    _verifyOptions = PuzzleUniquenessVerifier.Options.Default;
                    _verifyOptions.Save();
                    GUI.FocusControl(null);
                }
            }
        }
        EditorGUILayout.Space(2);
    }

    private void RunVerification()
    {
        // The window may have been closed, or play mode entered, between the click and this callback.
        if (this == null || Application.isPlaying)
        {
            _verifyQueued = false;
            return;
        }

        // The verifier temporarily rearranges occupancy state, which can
        // publish object-change events — don't let it mark its own result stale.
        _suppressChangeEvents = true;
        try
        {
            _verifyResult      = PuzzleUniquenessVerifier.Run(_verifyOptions, interactive: true);
            _hasVerifyResult   = true;
            _verifyStale       = false;
            _verifyScenePath   = SceneManager.GetActiveScene().path;
            _verifyScriptStamp = CurrentScriptStamp();
            _verifyDetails     = _verifyResult.outcome != PuzzleUniquenessVerifier.Outcome.Unique;
            Debug.Log(_verifyResult.report);
        }
        finally
        {
            _verifyQueued = false;
            ReleaseChangeSuppression();
            Repaint();
        }
    }

    private void DrawLayoutRow(int index, PuzzleUniquenessVerifier.LayoutInfo layout)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        string header = $"Layout {index + 1} — killer(s): [{string.Join(", ", layout.killers)}]"
            + (layout.matchesAuthored ? "  (authored solution)" : "");
        GUILayout.Label(header, EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("Apply", EditorStyles.miniButton, GUILayout.Width(52)))
                ApplyLayout(layout);
        EditorGUILayout.EndHorizontal();

        var lines = new System.Text.StringBuilder();
        foreach (var p in layout.placements)
            lines.AppendLine($"{p.suspect}: ({p.row},{p.col}) section {p.section}");
        string text = lines.ToString().TrimEnd('\n');
        EditorGUILayout.SelectableLabel(text,
            EditorStyles.miniLabel,
            GUILayout.MinHeight(EditorStyles.miniLabel.CalcHeight(new GUIContent(text), position.width - 40)));

        EditorGUILayout.EndVertical();
    }

    // Moves every suspect to the layout's cells and adopts it as the authored
    // solution (SolutionPosition re-authors on edit-mode moves by design).
    // Fully undoable in a single step.
    private void ApplyLayout(PuzzleUniquenessVerifier.LayoutInfo layout)
    {
        var entries = layout.placements
            .Select(p => new BoardLayoutEntry { objectName = p.suspect, row = p.row, col = p.col })
            .ToList();

        // Applying doesn't invalidate the verdict: layouts don't depend on
        // current suspect positions — keep the result fresh.
        _suppressChangeEvents = true;
        try
        {
            var report = BoardLayoutIO.Apply(entries, "Apply Puzzle Layout", strict: true, logTag: "PuzzleVerifier");
            if (report.aborted) return;

            if (_hasVerifyResult && _verifyResult.layouts != null)
                foreach (var li in _verifyResult.layouts)
                    li.matchesAuthored = ReferenceEquals(li, layout);
        }
        finally
        {
            ReleaseChangeSuppression();
        }
        Repaint();
    }

    // Our own scene writes must not mark the result they just produced stale.
    // ObjectChangeEvents.changesPublished can flush after the delayCall queue runs,
    // so hold the guard for one extra tick rather than clearing it on the next one.
    private void ReleaseChangeSuppression()
        => EditorApplication.delayCall += ()
            => EditorApplication.delayCall += () => _suppressChangeEvents = false;

    private static void DrawSeparator()
    {
        EditorGUILayout.Space(2);
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rect, ColorSeparator);
        EditorGUILayout.Space(2);
    }
}
