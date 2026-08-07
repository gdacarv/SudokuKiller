using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelRulesWindow : EditorWindow
{
    // ── Suspect entry ─────────────────────────────────────────────────
    private class SuspectEntry
    {
        public GameObject go;
        public bool isVictim;
        public SerializedObject soDraggable;
        public SerializedObject soName;   // NameLabel — may be null
        public SerializedObject soClues;  // CluesTooltip — may be null
    }

    // ── Scene refs ────────────────────────────────────────────────────
    private GridManager  _gridManager;
    private GeneralTips  _generalTips;

    // ── Serialized objects ────────────────────────────────────────────
    private SerializedObject _soGridManager;
    private SerializedObject _soGridOverlay;
    private SerializedObject _soGeneralTips;

    private readonly List<SuspectEntry>     _suspects  = new();
    private readonly Dictionary<int, bool>  _foldouts  = new();

    // ── UI state ──────────────────────────────────────────────────────
    private Vector2 _scroll;
    private bool    _gridFoldout  = false;
    private bool    _tipsFoldout  = false;
    private bool    _gridSizeFoldout = false;

    // ── Uniqueness verification state ─────────────────────────────────
    // Serialized so the last result survives domain reloads (play mode, recompiles).
    [SerializeField] private PuzzleUniquenessVerifier.VerificationResult _verifyResult;
    [SerializeField] private bool   _hasVerifyResult;
    [SerializeField] private bool   _verifyDetails;
    [SerializeField] private bool   _verifyStale;
    [SerializeField] private string _verifyScenePath;
    [SerializeField] private string _verifyScriptStamp;
    private Vector2 _verifyScroll;
    private bool    _suppressChangeEvents = false;

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

    // ── Colors ────────────────────────────────────────────────────────
    private static readonly Color ColorVictimHeader  = new(0.65f, 0.22f, 0.22f, 0.30f);
    private static readonly Color ColorSuspectHeader = new(0.25f, 0.30f, 0.50f, 0.20f);
    private static readonly Color ColorSectionHeader = new(0.20f, 0.20f, 0.20f, 0.20f);
    private static readonly Color ColorSeparator     = new(0.50f, 0.50f, 0.50f, 0.35f);

    // ──────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Level Rules")]
    public static void Open() => GetWindow<LevelRulesWindow>("Level Rules");

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        titleContent = new GUIContent("Level Rules", EditorGUIUtility.FindTexture("d_Search Icon"));
        EditorSceneManager.sceneOpened                  += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        Selection.selectionChanged                      += OnSelectionChanged;
        ObjectChangeEvents.changesPublished             += OnObjectsChanged;
        Undo.undoRedoPerformed                          += OnUndoRedo;

        // Coming back from a domain reload: drop a result that belongs to another
        // scene; mark it stale if scripts recompiled since it was computed.
        ClearVerifyResultIfSceneChanged();
        if (_hasVerifyResult && _verifyScriptStamp != CurrentScriptStamp())
            _verifyStale = true;

        Discover();
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened                  -= OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        Selection.selectionChanged                      -= OnSelectionChanged;
        ObjectChangeEvents.changesPublished             -= OnObjectsChanged;
        Undo.undoRedoPerformed                          -= OnUndoRedo;
    }

    // Undo/redo can move suspects (e.g. undoing an applied layout) — keep the
    // "(authored solution)" tag on layout rows truthful.
    private void OnUndoRedo()
    {
        // ApplyLayout records SolutionPosition together with the transform, so undo
        // restores both at once and SolutionPosition.Update never sees the mismatch
        // that normally triggers its self-healing refresh — occupancy and violation
        // highlights would otherwise keep describing the undone layout.
        if (!Application.isPlaying)
        {
            var gm = FindFirstObjectByType<GridManager>();
            if (gm != null) gm.RefreshEditModeViolations();
        }

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

    private void OnSceneOpened(Scene scene, OpenSceneMode mode) { ClearVerifyResultIfSceneChanged(); Discover(); }
    private void OnActiveSceneChanged(Scene prev, Scene next)   { ClearVerifyResultIfSceneChanged(); Discover(); }

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

    // Syncs suspect foldouts with the active Hierarchy/Scene selection:
    // the selected suspect opens, all others collapse.
    private void OnSelectionChanged()
    {
        var selected = Selection.activeGameObject;
        if (selected == null) return;

        var match = _suspects.FirstOrDefault(e => e.go == selected);
        if (match == null) return;

        foreach (var e in _suspects)
            _foldouts[e.go.GetInstanceID()] = e.go == selected;

        Repaint();
    }

    // ── Discovery ─────────────────────────────────────────────────────

    private void Discover()
    {
        _gridManager  = FindFirstObjectByType<GridManager>();
        _generalTips  = FindFirstObjectByType<GeneralTips>();

        _soGridManager = _gridManager != null ? new SerializedObject(_gridManager) : null;
        _soGeneralTips = _generalTips != null ? new SerializedObject(_generalTips) : null;
        RebuildGridOverlaySO();

        _suspects.Clear();

        // Sort: Victim first, then alphabetically
        var sorted = FindObjectsByType<Draggable>(FindObjectsSortMode.None)
            .OrderBy(d => d.gameObject.name != "Victim")
            .ThenBy(d => d.gameObject.name);

        foreach (var d in sorted)
        {
            var go        = d.gameObject;
            var nameLabel = go.GetComponentInChildren<NameLabel>(true);
            var clues     = go.GetComponent<CluesTooltip>();

            _suspects.Add(new SuspectEntry
            {
                go          = go,
                isVictim    = go.name == "Victim",
                soDraggable = new SerializedObject(d),
                soName      = nameLabel != null ? new SerializedObject(nameLabel) : null,
                soClues     = clues     != null ? new SerializedObject(clues)     : null,
            });

            int id = go.GetInstanceID();
            if (!_foldouts.ContainsKey(id))
                _foldouts[id] = false;
        }

        Repaint();
    }

    private void RebuildGridOverlaySO()
    {
        var overlay    = _gridManager != null ? _gridManager.gridOverlay : null;
        _soGridOverlay = overlay != null ? new SerializedObject(overlay) : null;
    }

    // ── Stale guard ───────────────────────────────────────────────────

    private bool AnyStale()
    {
        if (_gridManager == null && FindFirstObjectByType<GridManager>() != null) return true;
        if (_generalTips == null && FindFirstObjectByType<GeneralTips>() != null) return true;
        foreach (var e in _suspects)
            if (e.go == null) return true;
        return false;
    }

    // ── Main GUI ──────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (AnyStale()) Discover();

        // Rebuild GridOverlay SO if the gridOverlay reference changed
        var currentOverlay = _gridManager != null ? _gridManager.gridOverlay : null;
        if (_soGridOverlay == null && currentOverlay != null ||
            _soGridOverlay != null && (UnityEngine.Object)_soGridOverlay.targetObject != currentOverlay)
            RebuildGridOverlaySO();

        GUILayout.Label("Level Rules", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh", GUILayout.Height(22)))
            Discover();
        EditorGUILayout.HelpBox("Use Refresh after adding or removing suspects from the scene. Scene changes are detected automatically.", MessageType.None);

        DrawSeparator();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawGridSection();
        EditorGUILayout.Space(6);
        DrawGeneralTipsSection();
        EditorGUILayout.Space(8);
        DrawSuspectsSection();

        EditorGUILayout.EndScrollView();

        DrawVerifySection();
    }

    // ── Puzzle uniqueness footer ──────────────────────────────────────

    private void DrawVerifySection()
    {
        DrawSeparator();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Verify Puzzle Uniqueness", GUILayout.Height(24)))
            {
                // The verifier temporarily rearranges occupancy state, which can
                // publish object-change events — don't let it mark its own result stale.
                _suppressChangeEvents = true;
                try
                {
                    _verifyResult      = PuzzleUniquenessVerifier.Run();
                    _hasVerifyResult   = true;
                    _verifyStale       = false;
                    _verifyScenePath   = SceneManager.GetActiveScene().path;
                    _verifyScriptStamp = CurrentScriptStamp();
                    _verifyDetails     = _verifyResult.outcome != PuzzleUniquenessVerifier.Outcome.Unique;
                    Debug.Log(_verifyResult.report);
                }
                finally
                {
                    ReleaseChangeSuppression();
                }
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Verification runs in Edit Mode only.", MessageType.None);

        if (!_hasVerifyResult) return;
        var result = _verifyResult;

        if (_verifyStale)
            EditorGUILayout.HelpBox("Stale — the scene or assets changed since this result. Re-verify.", MessageType.Warning);

        var messageType = result.outcome switch
        {
            PuzzleUniquenessVerifier.Outcome.Unique       => MessageType.Info,
            PuzzleUniquenessVerifier.Outcome.KillerUnique => MessageType.Warning,
            PuzzleUniquenessVerifier.Outcome.Inconclusive => MessageType.Warning,
            _                                             => MessageType.Error,
        };
        using (new EditorGUI.DisabledScope(_verifyStale))
            EditorGUILayout.HelpBox(result.verdict, messageType);

        _verifyDetails = EditorGUILayout.Foldout(_verifyDetails, "Details", true);
        if (_verifyDetails)
        {
            _verifyScroll = EditorGUILayout.BeginScrollView(_verifyScroll, GUILayout.MaxHeight(220));

            string summary = result.summary ?? result.report;
            EditorGUILayout.SelectableLabel(summary,
                EditorStyles.miniLabel,
                GUILayout.MinHeight(EditorStyles.miniLabel.CalcHeight(new GUIContent(summary), position.width - 30)));

            if (result.layouts != null)
                for (int i = 0; i < result.layouts.Count; i++)
                    DrawLayoutRow(i, result.layouts[i]);

            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.Space(2);
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
        var gm = FindFirstObjectByType<GridManager>();
        if (gm == null)
        {
            Debug.LogWarning("[LevelRules] Cannot apply layout — no GridManager in scene.");
            return;
        }

        // Layouts address suspects by name, so duplicates are unresolvable — report
        // them the same way a missing suspect is reported instead of throwing.
        var byName = new Dictionary<string, Draggable>();
        foreach (var d in FindObjectsByType<Draggable>(FindObjectsSortMode.None))
        {
            if (d.GetComponent<SolutionPosition>() == null) continue;
            if (byName.ContainsKey(d.name))
            {
                Debug.LogWarning($"[LevelRules] Cannot apply layout — more than one suspect is named '{d.name}'. Give them distinct names and re-verify.");
                return;
            }
            byName[d.name] = d;
        }

        foreach (var p in layout.placements)
            if (!byName.ContainsKey(p.suspect))
            {
                Debug.LogWarning($"[LevelRules] Cannot apply layout — suspect '{p.suspect}' not found in scene (was it renamed?).");
                return;
            }

        // Applying doesn't invalidate the verdict: layouts don't depend on
        // current suspect positions — keep the result fresh.
        _suppressChangeEvents = true;
        try
        {
            // Close whatever group is still open, so collapsing below cannot swallow
            // the user's previous edit into this one undo step.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Puzzle Layout");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var p in layout.placements)
            {
                var d = byName[p.suspect];
                var sp = d.GetComponent<SolutionPosition>();
                Undo.RecordObject(d.transform, "Apply Puzzle Layout");
                Undo.RecordObject(sp, "Apply Puzzle Layout");
                d.transform.position = gm.GetCellCenter(p.row, p.col);
                sp.solutionRow = p.row;
                sp.solutionCol = p.col;
                EditorUtility.SetDirty(sp);
            }

            Undo.CollapseUndoOperations(undoGroup);
            gm.RefreshEditModeViolations();

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

    // ── Helpers ───────────────────────────────────────────────────────

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

    // Draws a foldout header row with a tinted background rect.
    // Returns the new foldout state.
    private static bool DrawHeaderFoldout(bool open, string label, Color bgColor)
    {
        Rect rowRect = EditorGUILayout.BeginHorizontal();
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rowRect, bgColor);
        bool newOpen = EditorGUILayout.Foldout(open, label, true, EditorStyles.foldoutHeader);
        EditorGUILayout.EndHorizontal();
        return newOpen;
    }

    // ── Grid & Board section ──────────────────────────────────────────

    private void DrawGridSection()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        _gridFoldout = DrawHeaderFoldout(_gridFoldout, "Grid & Board Rules", ColorSectionHeader);

        if (_gridFoldout)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                if (_soGridManager == null)
                {
                    EditorGUILayout.HelpBox("No GridManager found in scene.", MessageType.Warning);
                }
                else
                {
                    _soGridManager.Update();
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("gridOverlay"));
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("boardRules"),  true);
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("killerRules"), true);
                    EditorGUILayout.Space(2);
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("preventInvalidPlacement"));
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("highlightRuleViolations"));
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("startAtSolutionPositions"));
                    EditorGUILayout.PropertyField(_soGridManager.FindProperty("showGridOverlay"));
                    _soGridManager.ApplyModifiedProperties();

                    if (_soGridOverlay != null)
                    {
                        EditorGUILayout.Space(4);
                        _gridSizeFoldout = EditorGUILayout.Foldout(_gridSizeFoldout, "Grid Size", true, EditorStyles.foldoutHeader);
                        if (_gridSizeFoldout)
                        {
                            _soGridOverlay.Update();
                            EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("rows"));
                            EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cols"));
                            EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cellWidth"));
                            EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cellHeight"));
                            _soGridOverlay.ApplyModifiedProperties();
                        }
                    }
                }
            }
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
    }

    // ── General Tips section ──────────────────────────────────────────

    private void DrawGeneralTipsSection()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        _tipsFoldout = DrawHeaderFoldout(_tipsFoldout, "General Tips", ColorSectionHeader);

        if (_tipsFoldout)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                if (_soGeneralTips == null)
                {
                    EditorGUILayout.HelpBox("No GeneralTips found in scene.", MessageType.Warning);
                }
                else
                {
                    _soGeneralTips.Update();
                    EditorGUILayout.PropertyField(_soGeneralTips.FindProperty("tips"),    true);
                    EditorGUILayout.PropertyField(_soGeneralTips.FindProperty("columns"));
                    if (_soGeneralTips.ApplyModifiedProperties())
                        _generalTips.Rebuild();
                }
            }
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
    }

    // ── Suspects section ──────────────────────────────────────────────

    private void DrawSuspectsSection()
    {
        GUILayout.Label("Suspects", EditorStyles.boldLabel);
        DrawSeparator();

        if (_suspects.Count == 0)
        {
            EditorGUILayout.HelpBox("No Draggable GameObjects found in scene.", MessageType.Warning);
            return;
        }

        foreach (var entry in _suspects)
        {
            if (entry.go == null) continue;
            DrawSuspectFoldout(entry);
            EditorGUILayout.Space(4);
        }
    }

    private void DrawSuspectFoldout(SuspectEntry entry)
    {
        int    id    = entry.go.GetInstanceID();
        if (!_foldouts.ContainsKey(id)) _foldouts[id] = false;

        string label  = entry.isVictim ? $"{entry.go.name}  ★" : entry.go.name;
        Color  bgColor = entry.isVictim ? ColorVictimHeader : ColorSuspectHeader;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        // Header row with tinted background + Select button
        Rect headerRect = EditorGUILayout.BeginHorizontal();
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(headerRect, bgColor);
        bool open = EditorGUILayout.Foldout(_foldouts[id], label, true);
        _foldouts[id] = open;
        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(52)))
        {
            Selection.activeGameObject = entry.go;
            EditorGUIUtility.PingObject(entry.go);
        }
        EditorGUILayout.EndHorizontal();

        if (open)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                // ── Localized Name ────────────────────────────────
                if (entry.soName != null)
                {
                    GUILayout.Label("Name", EditorStyles.miniBoldLabel);
                    entry.soName.Update();
                    EditorGUILayout.PropertyField(entry.soName.FindProperty("localizedName"));
                    entry.soName.ApplyModifiedProperties();
                }
                else
                {
                    EditorGUILayout.HelpBox("No NameLabel component.", MessageType.None);
                }

                EditorGUILayout.Space(2);

                // ── Clue Lines ────────────────────────────────────
                if (entry.soClues != null)
                {
                    GUILayout.Label("Clue Lines", EditorStyles.miniBoldLabel);
                    entry.soClues.Update();
                    EditorGUILayout.PropertyField(entry.soClues.FindProperty("lines"), true);
                    entry.soClues.ApplyModifiedProperties();
                }
                else
                {
                    EditorGUILayout.HelpBox("No CluesTooltip component.", MessageType.None);
                }

                EditorGUILayout.Space(2);

                // ── Rules ─────────────────────────────────────────
                GUILayout.Label("Rules", EditorStyles.miniBoldLabel);
                entry.soDraggable.Update();
                EditorGUILayout.PropertyField(entry.soDraggable.FindProperty("rules"), true);
                entry.soDraggable.ApplyModifiedProperties();
            }
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
    }
}
