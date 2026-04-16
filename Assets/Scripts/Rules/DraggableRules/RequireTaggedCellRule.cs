using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Require Tagged Cell")]
public class RequireTaggedCellRule : DraggableRule
{
    [Tooltip("At least one of these tags must exist on a GridEntity at the target cell (OR logic).")]
    public List<GridEntity.TagEntry> cellTags = new();

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        var matches = manager.FindEntitiesWithTags(cellTags);
        foreach (var entity in matches)
            if (entity.Row == row && entity.Col == col)
                return true;
        return false;
    }
}
