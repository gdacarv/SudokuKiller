using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SolutionPosition : MonoBehaviour
{
    public GridManager gridManager;
    public Vector3 uiPosition;

    [HideInInspector] public int solutionRow;
    [HideInInspector] public int solutionCol;

    public int SolutionRow => solutionRow;
    public int SolutionCol => solutionCol;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private bool IsAncestorBeingMoved()
    {
        var p = transform.parent;
        while (p != null)
        {
            if (UnityEditor.Selection.Contains(p.gameObject))
                return true;
            p = p.parent;
        }
        return false;
    }
#endif

    void Update()
    {
        if (Application.isPlaying) return;

        if (gridManager == null) return;

#if UNITY_EDITOR
        if (IsAncestorBeingMoved()) return;
#endif

        var cell = gridManager.WorldToCell(transform.position);
        int newRow = cell?.y ?? -1;
        int newCol = cell?.x ?? -1;

        if (newRow != solutionRow || newCol != solutionCol)
        {
            solutionRow = newRow;
            solutionCol = newCol;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (gridManager.highlightRuleViolations)
                gridManager.RefreshEditModeViolations();
#endif
        }

        if (cell != null)
            transform.position = gridManager.GetCellCenter(solutionRow, solutionCol);
    }

    [ContextMenu("Save Current as UI Position")]
    public void SaveCurrentAsUIPosition()
    {
        uiPosition = transform.position;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public bool IsInSolutionCell()
    {
        if (gridManager == null) return false;

        var cell = gridManager.WorldToCell(transform.position);
        if (!cell.HasValue) return false;

        return cell.Value.x == solutionCol && cell.Value.y == solutionRow;
    }
}
