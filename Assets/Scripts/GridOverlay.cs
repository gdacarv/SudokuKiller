using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GridOverlay : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int rows = 9;
    public int cols = 9;

    [Header("Cell Size (world units)")]
    public float cellWidth  = 0.48f;
    public float cellHeight = 0.48f;

    [Header("Appearance")]
    public Color  lineColor        = Color.black;
    public float  lineWidth        = 0.002f;
    public int    sortingOrder     = 10;
    public string sortingLayerName = "Default";

    [Header("Offset (world units, relative to this transform)")]
    public Vector2 offset = Vector2.zero;

    [SerializeField, HideInInspector] private bool _linesVisible = true;

    // Persistent root for the generated line objects. Serialized as a real scene object
    // so Unity keeps its Scene-view visibility/picking state (the eye / hand icons)
    // across play mode, domain reload, and editor restarts. Only this root persists;
    // the Line children are rebuilt every time and stay HideFlags.DontSave.
    [SerializeField, HideInInspector] private Transform _linesRoot;

    private List<LineRenderer> _lines = new List<LineRenderer>();
    private HashSet<Vector2Int> _hiddenCells = new HashSet<Vector2Int>();

    void OnEnable()  => BuildGrid();
    void OnDisable() => DestroyGrid();

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) BuildGrid();
        };
#endif
    }

    void BuildGrid()
    {
        DestroyGrid();

        EnsureLinesRoot();
        _linesRoot.gameObject.SetActive(_linesVisible);

        float totalW = cellWidth  * Mathf.Max(cols, 1);
        float totalH = cellHeight * Mathf.Max(rows, 1);

        Vector3 origin = transform.position + new Vector3(offset.x, offset.y, 0f);

        float left   = origin.x - totalW * 0.5f;
        float bottom = origin.y - totalH * 0.5f;
        float z      = origin.z;

        // Vertical segments: boundary c (0..cols), row r (0..rows-1)
        for (int c = 0; c <= cols; c++)
        {
            float x = left + c * cellWidth;
            for (int r = 0; r < rows; r++)
            {
                bool leftVisible  = c > 0    && !_hiddenCells.Contains(new Vector2Int(c - 1, r));
                bool rightVisible = c < cols && !_hiddenCells.Contains(new Vector2Int(c, r));
                if (leftVisible || rightVisible)
                {
                    float y0 = bottom + r * cellHeight;
                    float y1 = bottom + (r + 1) * cellHeight;
                    CreateLine(new Vector3(x, y0, z), new Vector3(x, y1, z));
                }
            }
        }

        // Horizontal segments: boundary r (0..rows), col c (0..cols-1)
        for (int r = 0; r <= rows; r++)
        {
            float y = bottom + r * cellHeight;
            for (int c = 0; c < cols; c++)
            {
                bool bottomVisible = r > 0    && !_hiddenCells.Contains(new Vector2Int(c, r - 1));
                bool topVisible    = r < rows && !_hiddenCells.Contains(new Vector2Int(c, r));
                if (bottomVisible || topVisible)
                {
                    float x0 = left + c * cellWidth;
                    float x1 = left + (c + 1) * cellWidth;
                    CreateLine(new Vector3(x0, y, z), new Vector3(x1, y, z));
                }
            }
        }

#if UNITY_EDITOR
        PropagateSceneVisibility();
#endif
    }

    public void HideCell(int row, int col) => _hiddenCells.Add(new Vector2Int(col, row));

    public void RefreshGrid() => BuildGrid();

    public void SetVisible(bool visible)
    {
        _linesVisible = visible;
        if (_linesRoot != null)
            _linesRoot.gameObject.SetActive(visible);
    }

    // Finds or creates the persistent "_GridLines" child. Kept as a normal (saved) scene
    // object so Unity persists its Scene visibility / picking toggles on its own.
    void EnsureLinesRoot()
    {
        if (_linesRoot == null)
        {
            var existing = transform.Find("_GridLines");
            if (existing != null)
                _linesRoot = existing;
        }

        if (_linesRoot == null)
        {
            var go = new GameObject("_GridLines");
            _linesRoot = go.transform;
            _linesRoot.SetParent(transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        _linesRoot.localPosition = Vector3.zero;
        _linesRoot.localRotation = Quaternion.identity;
        _linesRoot.localScale    = Vector3.one;
    }

    void CreateLine(Vector3 start, Vector3 end)
    {
        var go = new GameObject("Line");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(_linesRoot, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = true;
        lr.positionCount    = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth       = lineWidth;
        lr.endWidth         = lineWidth;
        lr.material         = new Material(Shader.Find("Sprites/Default"));
        lr.startColor       = lineColor;
        lr.endColor         = lineColor;
        lr.sortingOrder     = sortingOrder;
        lr.sortingLayerName = sortingLayerName;

        _lines.Add(lr);
    }

    void DestroyGrid()
    {
        _lines.Clear();

        // Remove any stale "_GridLines" siblings from older builds, but keep the
        // persistent root we now reuse.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "_GridLines" && child != _linesRoot)
                DestroyObject(child.gameObject);
        }

        if (_linesRoot == null)
            return;

        // Clear the root's children (the generated Line objects); keep the root itself.
        for (int i = _linesRoot.childCount - 1; i >= 0; i--)
            DestroyObject(_linesRoot.GetChild(i).gameObject);
    }

    static void DestroyObject(GameObject go)
    {
        if (go == null)
            return;
        if (Application.isPlaying)
        {
            Destroy(go);
        }
        else
        {
#if UNITY_EDITOR
            DestroyImmediate(go);
#else
            Destroy(go);
#endif
        }
    }

#if UNITY_EDITOR
    // Mirror the root's own (Unity-persisted) Scene visibility / picking state onto the
    // freshly created Line children, which are what actually render and receive picks.
    void PropagateSceneVisibility()
    {
        if (_linesRoot == null)
            return;

        var svm = UnityEditor.SceneVisibilityManager.instance;
        var go  = _linesRoot.gameObject;

        if (svm.IsHidden(go))
            svm.Hide(go, true);
        if (svm.IsPickingDisabled(go))
            svm.DisablePicking(go, true);
    }
#endif
}
