using UnityEditor;
using UnityEngine;

public class GridPainterWindow : EditorWindow
{
    private enum Mode { Off, Paint, Erase }

    private GameObject _prefab;
    private int _sectionId = 0;
    private Mode _mode = Mode.Off;
    private bool _replaceExisting = true;
    private GridManager _gridManager;

    private Vector2Int _lastPaintedCell = new Vector2Int(-1, -1);

    [MenuItem("Tools/Grid Painter")]
    public static void Open() => GetWindow<GridPainterWindow>("Grid Painter");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("Grid Painter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _gridManager = (GridManager)EditorGUILayout.ObjectField("Grid Manager", _gridManager, typeof(GridManager), allowSceneObjects: true);

        if (_gridManager == null)
        {
            _gridManager = FindFirstObjectByType<GridManager>();
            if (_gridManager != null) Repaint();
        }

        EditorGUILayout.Space();
        _prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _prefab, typeof(GameObject), allowSceneObjects: false);
        _sectionId = EditorGUILayout.IntField("Section ID", _sectionId);
        _replaceExisting = EditorGUILayout.Toggle("Replace Existing", _replaceExisting);

        EditorGUILayout.Space();
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = _mode switch
        {
            Mode.Paint => new Color(0.4f, 1f, 0.6f),
            Mode.Erase => new Color(1f, 0.4f, 0.4f),
            _ => new Color(0.85f, 0.85f, 0.85f),
        };
        _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "OFF", "Paint", "Erase" }, GUILayout.Height(30));
        GUI.backgroundColor = prev;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            _mode == Mode.Off
                ? "Grid Painter is OFF — scene interaction works normally."
                : "Click or drag in the Scene view to paint/erase cells.\nHold the mouse button and drag to paint multiple cells.",
            MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_gridManager == null) return;
        if (_mode == Mode.Off) return;

        Event e = Event.current;

        // Consume left mouse button drag and click events while window is active
        bool isPaint = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0;
        if (!isPaint) return;

        // Prevent default scene selection behavior
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        Vector3 worldPos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        worldPos.z = 0f;

        Vector2Int? cellNullable = _gridManager.WorldToCell(worldPos);
        if (cellNullable == null) return;

        Vector2Int cell = cellNullable.Value;

        // Skip if we already painted this cell during this drag stroke
        if (cell == _lastPaintedCell) return;
        _lastPaintedCell = cell;

        if (_mode == Mode.Erase)
            EraseCell(cell);
        else
            PaintCell(cell);

        e.Use();
        sceneView.Repaint();
    }

    private void PaintCell(Vector2Int cell)
    {
        if (_prefab == null)
        {
            Debug.LogWarning("[GridPainter] No prefab selected.");
            return;
        }

        // Always replace same prefab; optionally replace different prefabs too
        if (_replaceExisting)
            EraseCell(cell);
        else
            EraseCell(cell, samePrefabOnly: true);

        Vector3 pos = _gridManager.GetCellCenter(cell.y, cell.x);
        pos.z = 0f;

        Transform parent = _gridManager.transform.Find("_GridEntities") ?? _gridManager.transform;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, parent);
        go.transform.position = pos;
        go.name = $"{_prefab.name} [{cell.y},{cell.x}]";

        // Auto-assign sectionId if the prefab has a SectionMarker
        var sm = go.GetComponent<SectionMarker>();
        if (sm != null)
        {
            sm.sectionId = _sectionId;
            EditorUtility.SetDirty(go);
        }

        Undo.RegisterCreatedObjectUndo(go, $"Paint {_prefab.name}");
    }

    private void EraseCell(Vector2Int cell, bool samePrefabOnly = false)
    {
        Vector3 pos = _gridManager.GetCellCenter(cell.y, cell.x);
        pos.z = 0f;

        var markers = _gridManager.GetComponentsInChildren<GridEntityMarker>(includeInactive: true);
        foreach (var m in markers)
        {
            if (Vector2.Distance(m.transform.position, pos) >= 0.01f) continue;
            if (samePrefabOnly && PrefabUtility.GetCorrespondingObjectFromSource(m.gameObject) != _prefab) continue;
            Undo.DestroyObjectImmediate(m.gameObject);
        }
    }
}
