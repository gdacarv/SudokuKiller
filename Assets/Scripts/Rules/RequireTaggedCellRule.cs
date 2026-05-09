using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Require Tagged Cell")]
public class RequireTaggedCellRule : Rule
{
    [Tooltip("All these tags must exist on a GridEntity at the target cell (AND logic).")]
    public List<GridEntity.TagEntry> cellTags = new();

    [Tooltip("Can't have a GridEntity at the target cell (Negation)")]
    public bool negated = false;
    
    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        var matches = manager.FindEntitiesWithTags(cellTags);
        foreach (var entity in matches)
            if (entity.Row == row && entity.Col == col)
                return !negated;
        return negated;
    }
}
