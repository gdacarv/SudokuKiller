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

    private List<LineRenderer> _lines = new List<LineRenderer>();
    private HashSet<Vector2Int> _hiddenCells = new HashSet<Vector2Int>();
    private GameObject _linesRoot;

    void OnEnable()   => BuildGrid();
    void OnDisable()  => DestroyGrid();
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

        _linesRoot = new GameObject("_GridLines");
        _linesRoot.transform.SetParent(transform, false);
        _linesRoot.transform.localPosition = Vector3.zero;
        _linesRoot.transform.localRotation = Quaternion.identity;
        _linesRoot.transform.localScale    = Vector3.one;
        _linesRoot.hideFlags = HideFlags.DontSave;

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
    }

    public void HideCell(int row, int col) => _hiddenCells.Add(new Vector2Int(col, row));

    public void RefreshGrid() => BuildGrid();

    public void SetVisible(bool visible)
    {
        if (_linesRoot != null)
            _linesRoot.SetActive(visible);
    }

    void CreateLine(Vector3 start, Vector3 end)
    {
        var go = new GameObject("Line");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(_linesRoot.transform, false);

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
        if (_linesRoot != null)
        {
            if (Application.isPlaying)
                Destroy(_linesRoot);
            else
            {
#if UNITY_EDITOR
                DestroyImmediate(_linesRoot);
#endif
            }
            _linesRoot = null;
        }
        // Also destroy any stale _GridLines children left from previous runs
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "_GridLines")
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                {
#if UNITY_EDITOR
                    DestroyImmediate(child.gameObject);
#endif
                }
            }
        }
    }
}
