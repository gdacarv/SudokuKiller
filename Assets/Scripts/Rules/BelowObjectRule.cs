using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Below Object")]
public class BelowObjectRule : DraggableRule
{
    [Tooltip("Tags identifying the target object(s) (AND logic — entity must match all entries)")]
    public List<GridEntity.TagEntry> targetTags = new();

    [Tooltip("True: must be below ALL matching entities. False: must be below ANY matching entity.")]
    public bool requireAll = true;

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        var targets = manager.FindEntitiesWithTags(targetTags);
        if (targets.Count == 0) return true; // no target on grid yet — no constraint

        if (requireAll)
        {
            foreach (var t in targets)
                if (row >= t.Row) return false;
            return true;
        }
        else
        {
            foreach (var t in targets)
                if (row < t.Row) return true;
            return false;
        }
    }
}
