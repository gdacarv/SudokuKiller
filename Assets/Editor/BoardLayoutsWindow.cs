using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Save the position of every draggable as a named preset and restore it later in one
// undoable step. Presets are BoardLayoutPreset assets under Assets/Levels/Layouts/<Scene>/.
public class BoardLayoutsWindow : EditorWindow
{
    private const string RootFolder    = "Assets/Levels/Layouts";
    private const string RenameControl = "BoardLayoutRename";

    private static readonly Color ColorSeparator = new(0.50f, 0.50f, 0.50f, 0.35f);

    [SerializeField] private bool _showOtherScenes;

    // ── Cached scene / project state ──────────────────────────────────
    private readonly List<BoardLayoutPreset> _presets = new();
    private List<BoardLayoutEntry> _live = new();
    private string _liveError;

    // ── UI state ──────────────────────────────────────────────────────
    private Vector2 _scroll;
    private string  _renamingPath;
    private string  _renameBuffer;
    private bool    _focusRename;
    private bool    _renameHadFocus;
    private string  _notePath;

    private BoardLayoutIO.ApplyReport _report;
    private string _reportTitle;
    private string _statusMessage;

    // ──────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Board Layouts")]
    public static void Open() => GetWindow<BoardLayoutsWindow>("Board Layouts");

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        titleContent = new GUIContent("Board Layouts", EditorGUIUtility.FindTexture("d_Grid Icon"));
        EditorSceneManager.sceneOpened                  += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        Undo.undoRedoPerformed                          += RefreshAll;

        RefreshAll();
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened                  -= OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        Undo.undoRedoPerformed                          -= RefreshAll;
        AssetDatabase.SaveAssets();   // persists any note edits
    }

    private void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshAll();
    private void OnActiveSceneChanged(Scene from, Scene to)     => RefreshAll();

    // Polled so the "current" marker follows drags without hooking every scene change.
    private void OnInspectorUpdate()
    {
        if (RefreshLive()) Repaint();
    }

    private void OnProjectChange()
    {
        RefreshPresets();
        Repaint();
    }

    // ── Cache refresh ─────────────────────────────────────────────────

    private void RefreshAll()
    {
        _report = null;
        RefreshLive();
        RefreshPresets();
        Repaint();
    }

    // Returns true when the board (or its error state) differs from the last read.
    private bool RefreshLive()
    {
        if (Application.isPlaying) return false;

        BoardLayoutIO.TrySnapshot(out var entries, out var error);
        bool changed = error != _liveError || !BoardLayoutIO.Matches(entries, _live);
        _live      = entries;
        _liveError = error;
        return changed;
    }

    private void RefreshPresets()
    {
        _presets.Clear();

        string search = _showOtherScenes ? RootFolder : SceneFolder();
        if (!AssetDatabase.IsValidFolder(search)) return;

        foreach (var guid in AssetDatabase.FindAssets("t:BoardLayoutPreset", new[] { search }))
        {
            var preset = AssetDatabase.LoadAssetAtPath<BoardLayoutPreset>(AssetDatabase.GUIDToAssetPath(guid));
            if (preset != null) _presets.Add(preset);
        }

        // Newest first, so a fresh quick-save lands at the top of the list.
        _presets.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(b.capturedUtc, a.capturedUtc);
            return c != 0 ? c : string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
        });
    }

    // ── GUI ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Board Layouts works in edit mode only.", MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            DrawHeader();
            DrawSeparator();
            DrawSaveRow();
            DrawSeparator();
            DrawList();
            DrawStatus();
        }
    }

    private void DrawHeader()
    {
        GUILayout.Label("Board Layouts", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Scene: {SceneManager.GetActiveScene().name}   ·   {_live.Count} draggables",
            EditorStyles.miniLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(64))) RefreshAll();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Save captures where every draggable is now. Load moves them all back in one undoable step (Ctrl+Z reverts it).",
            MessageType.None);
    }

    private void DrawSaveRow()
    {
        using (new EditorGUI.DisabledScope(_liveError != null || _live.Count == 0))
        {
            if (GUILayout.Button("Save Current Layout", GUILayout.Height(24)))
                Defer(SaveCurrent);
        }

        if (_liveError != null)
            EditorGUILayout.HelpBox($"Can't read the board: {_liveError}.", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        _showOtherScenes = EditorGUILayout.ToggleLeft("Show layouts from other scenes", _showOtherScenes);
        if (EditorGUI.EndChangeCheck()) Defer(RefreshPresets);
    }

    private void DrawList()
    {
        if (_presets.Count == 0)
            EditorGUILayout.HelpBox("No saved layouts for this scene yet.", MessageType.None);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _presets.Count; i++)
            if (_presets[i] != null) DrawRow(_presets[i]);
        EditorGUILayout.EndScrollView();
    }

    private void DrawRow(BoardLayoutPreset preset)
    {
        string path      = AssetDatabase.GetAssetPath(preset);
        bool   isCurrent = _liveError == null && BoardLayoutIO.Matches(preset.placements, _live);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        // Marker for the layout that matches the board right now.
        if (isCurrent)
        {
            var light = EditorGUIUtility.IconContent("greenLight");
            var marker = light.image != null ? new GUIContent(light.image, "Matches the board as it is now")
                                             : new GUIContent("●", "Matches the board as it is now");
            GUILayout.Label(marker, GUILayout.Width(16), GUILayout.Height(16));
        }
        else
        {
            GUILayout.Space(20);
        }

        if (path == _renamingPath)
        {
            DrawRenameField(path);
        }
        else
        {
            EditorGUILayout.LabelField(preset.name, isCurrent ? EditorStyles.boldLabel : EditorStyles.label);
            var nameRect = GUILayoutUtility.GetLastRect();
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.clickCount == 2 && nameRect.Contains(e.mousePosition))
            {
                BeginRename(path, preset.name);
                e.Use();
            }
        }

        using (new EditorGUI.DisabledScope(isCurrent))
        {
            if (GUILayout.Button("Load", GUILayout.Width(52)))
                Defer(() => Load(preset));
        }

        var menuIcon = EditorGUIUtility.IconContent("_Menu");
        var menuContent = menuIcon.image != null ? new GUIContent(menuIcon.image, "More") : new GUIContent("…", "More");
        if (GUILayout.Button(menuContent, GUILayout.Width(26)))
            ShowMenu(preset, path);

        EditorGUILayout.EndHorizontal();

        string info = $"{preset.placements.Count} draggables  ·  {preset.capturedUtc} UTC";
        if (_showOtherScenes) info += $"  ·  {preset.sceneName}";
        EditorGUILayout.LabelField(info, EditorStyles.miniLabel);

        if (path == _notePath)
            DrawNoteEditor(preset);
        else if (!string.IsNullOrEmpty(preset.note))
            EditorGUILayout.LabelField(preset.note, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawRenameField(string path)
    {
        GUI.SetNextControlName(RenameControl);
        _renameBuffer = EditorGUILayout.TextField(_renameBuffer);

        bool focused = GUI.GetNameOfFocusedControl() == RenameControl;
        if (_focusRename)
        {
            if (focused) { _focusRename = false; _renameHadFocus = true; }
            else         { EditorGUI.FocusTextInControl(RenameControl); Repaint(); }
        }
        else if (focused)
        {
            _renameHadFocus = true;
        }

        var e = Event.current;
        if (e.type == EventType.KeyDown && focused)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.Use();
                Defer(() => CommitRename(path));
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Defer(() => { if (_renamingPath == path) _renamingPath = null; });
            }
        }
        else if (_renameHadFocus && !focused && !_focusRename && e.type == EventType.Repaint)
        {
            // Clicked elsewhere — keep what was typed.
            Defer(() => CommitRename(path));
        }
    }

    private void DrawNoteEditor(BoardLayoutPreset preset)
    {
        EditorGUI.BeginChangeCheck();
        string note = EditorGUILayout.TextArea(preset.note, GUILayout.MinHeight(36));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(preset, "Edit Board Layout Note");
            preset.note = note;
            EditorUtility.SetDirty(preset);
        }
        if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(52)))
        {
            AssetDatabase.SaveAssetIfDirty(preset);
            _notePath = null;
        }
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(_statusMessage))
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Warning);

        if (_report == null) return;

        var lines = new List<string>();
        if (_report.aborted)
        {
            lines.Add($"Could not load '{_reportTitle}': {_report.abortReason}");
        }
        else
        {
            lines.Add($"Loaded '{_reportTitle}': placed {_report.applied} draggable(s).");
            if (_report.missing.Count > 0)
                lines.Add($"Not found in scene (was it renamed?): {string.Join(", ", _report.missing)}");
            if (_report.unlisted.Count > 0)
                lines.Add($"In the scene but not in this layout (left where they are): {string.Join(", ", _report.unlisted)}");
            if (_report.outOfBounds.Count > 0)
                lines.Add($"Saved cell is outside the current grid (skipped): {string.Join(", ", _report.outOfBounds)}");
            if (_report.collisions.Count > 0)
                lines.Add($"Share a cell: {string.Join("; ", _report.collisions)}");
        }

        DrawSeparator();
        EditorGUILayout.HelpBox(string.Join("\n", lines), _report.HasIssues ? MessageType.Warning : MessageType.Info);
        if (GUILayout.Button("Dismiss", EditorStyles.miniButton, GUILayout.Width(64)))
            _report = null;
    }

    private static void DrawSeparator()
    {
        EditorGUILayout.Space(2);
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rect, ColorSeparator);
        EditorGUILayout.Space(2);
    }

    // ── Row menu ──────────────────────────────────────────────────────

    private void ShowMenu(BoardLayoutPreset preset, string path)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Rename"), false, () => Defer(() => BeginRename(path, preset.name)));

        if (_liveError == null)
            menu.AddItem(new GUIContent("Overwrite from Scene"), false, () => Defer(() => Overwrite(preset)));
        else
            menu.AddDisabledItem(new GUIContent("Overwrite from Scene"));

        menu.AddItem(new GUIContent("Duplicate"),    false, () => Defer(() => Duplicate(preset, path)));
        menu.AddItem(new GUIContent("Edit Note"),    false, () => Defer(() => _notePath = path));
        menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(preset));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete"),       false, () => Defer(() => Delete(preset, path)));
        menu.ShowAsContext();
    }

    // ── Actions (always invoked via Defer, never from inside OnGUI) ───

    private void Defer(System.Action action)
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            action();
            Repaint();
        };
    }

    private void SaveCurrent()
    {
        var preset = CreateInstance<BoardLayoutPreset>();
        if (!BoardLayoutIO.Capture(preset, out var error))
        {
            DestroyImmediate(preset);
            Debug.LogWarning($"[BoardLayouts] Cannot save layout — {error}.");
            return;
        }

        string folder = SceneFolder();
        EnsureFolder(folder);

        string path = NextDefaultPath(folder);
        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        RefreshPresets();

        // Land straight in rename mode: type over the default name, or Enter to keep it.
        BeginRename(path, preset.name);
        _scroll = Vector2.zero;
        _report = null;
        _statusMessage = null;
    }

    private void Load(BoardLayoutPreset preset)
    {
        if (preset == null || Application.isPlaying) return;

        _statusMessage = null;
        _reportTitle   = preset.name;
        _report        = BoardLayoutIO.Apply(preset.placements, $"Load Board Layout '{preset.name}'", strict: false);
        RefreshLive();
    }

    private void Overwrite(BoardLayoutPreset preset)
    {
        if (!EditorUtility.DisplayDialog("Overwrite layout",
                $"Replace '{preset.name}' with the board as it is now?", "Overwrite", "Cancel"))
            return;

        Undo.RecordObject(preset, "Overwrite Board Layout");
        if (!BoardLayoutIO.Capture(preset, out var error))
        {
            Debug.LogWarning($"[BoardLayouts] Cannot overwrite layout — {error}.");
            return;
        }
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssetIfDirty(preset);
        RefreshPresets();
    }

    private void Duplicate(BoardLayoutPreset preset, string path)
    {
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        string dst = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{preset.name} copy.asset");
        if (AssetDatabase.CopyAsset(path, dst)) RefreshPresets();
    }

    private void Delete(BoardLayoutPreset preset, string path)
    {
        if (!EditorUtility.DisplayDialog("Delete layout",
                $"Move '{preset.name}' to the trash?", "Delete", "Cancel"))
            return;

        if (_renamingPath == path) _renamingPath = null;
        if (_notePath == path)     _notePath = null;
        AssetDatabase.MoveAssetToTrash(path);
        RefreshPresets();
    }

    private void BeginRename(string path, string currentName)
    {
        _renamingPath   = path;
        _renameBuffer   = currentName;
        _focusRename    = true;
        _renameHadFocus = false;
    }

    private void CommitRename(string path)
    {
        // Enter and focus-loss can both queue a commit; only the first one counts.
        if (_renamingPath != path) return;
        _renamingPath = null;

        string newName = SanitizeFileName(_renameBuffer);
        if (string.IsNullOrEmpty(newName) || newName == Path.GetFileNameWithoutExtension(path)) return;

        string error = AssetDatabase.RenameAsset(path, newName);
        _statusMessage = string.IsNullOrEmpty(error) ? null : $"Could not rename: {error}";
        RefreshPresets();
    }

    // ── Paths ─────────────────────────────────────────────────────────

    private static string SceneFolder()
        => $"{RootFolder}/{SanitizeFileName(SceneManager.GetActiveScene().name, "Untitled")}";

    private static string NextDefaultPath(string folder)
    {
        for (int i = 1; ; i++)
        {
            string path = $"{folder}/Layout {i:000}.asset";
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) return path;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string SanitizeFileName(string name, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim();
        return name.Length == 0 ? fallback : name;
    }
}
