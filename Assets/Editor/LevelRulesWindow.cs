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
        Discover();
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened                  -= OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
    }

    private void OnSceneOpened(Scene scene, OpenSceneMode mode) => Discover();
    private void OnActiveSceneChanged(Scene prev, Scene next)   => Discover();

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
    }

    // ── Helpers ───────────────────────────────────────────────────────

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
                        GUILayout.Label("Grid Size", EditorStyles.miniBoldLabel);
                        _soGridOverlay.Update();
                        EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("rows"));
                        EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cols"));
                        EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cellWidth"));
                        EditorGUILayout.PropertyField(_soGridOverlay.FindProperty("cellHeight"));
                        _soGridOverlay.ApplyModifiedProperties();
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
