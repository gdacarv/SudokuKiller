using UnityEngine;

[ExecuteAlways]
public class SolutionPosition : MonoBehaviour
{
    public GridManager gridManager;
    public Vector3 uiPosition;

    [HideInInspector] public int solutionRow;
    [HideInInspector] public int solutionCol;

    public int SolutionRow => solutionRow;
    public int SolutionCol => solutionCol;

    void Update()
    {
        if (Application.isPlaying) return;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager == null) return;

        var cell = gridManager.WorldToCell(transform.position);
        if (cell == null) return;

        solutionCol = cell.Value.x;
        solutionRow = cell.Value.y;

        transform.position = gridManager.GetCellCenter(solutionRow, solutionCol);
    }

    [ContextMenu("Save Current as UI Position")]
    public void SaveCurrentAsUIPosition()
    {
        uiPosition = transform.position;
    }

    public bool IsInSolutionCell()
    {
        if (gridManager == null) return false;

        var cell = gridManager.WorldToCell(transform.position);
        if (!cell.HasValue) return false;

        return cell.Value.x == solutionCol && cell.Value.y == solutionRow;
    }
}
