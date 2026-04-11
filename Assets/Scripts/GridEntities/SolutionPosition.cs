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
#endif

    void Update()
    {
        if (Application.isPlaying) return;

        if (gridManager == null) return;

        var cell = gridManager.WorldToCell(transform.position);
        if (cell == null)
        {
            if (solutionRow != -1 || solutionCol != -1)
            {
                solutionRow = -1;
                solutionCol = -1;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                if (gridManager != null && gridManager.highlightRuleViolations)
                    gridManager.RefreshEditModeViolations();
#endif
            }
            return;
        }

        int newRow = cell.Value.y;
        int newCol = cell.Value.x;

        if (newRow != solutionRow || newCol != solutionCol)
        {
            solutionRow = newRow;
            solutionCol = newCol;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (gridManager != null && gridManager.highlightRuleViolations)
                gridManager.RefreshEditModeViolations();
#endif
        }

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
