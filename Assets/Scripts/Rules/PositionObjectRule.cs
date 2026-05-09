using System.Collections.Generic;
using UnityEngine;

public enum GridAxis { Vertical, Horizontal }

[CreateAssetMenu(menuName = "Sudoku/Rules/Position Object")]
public class PositionObjectRule : Rule
{
    [Tooltip("Tags identifying the target object(s) (AND logic — entity must match all entries)")]
    public List<GridEntity.TagEntry> targetTags = new();

    public GridAxis axis = GridAxis.Vertical;
    public PositionComparison comparison = PositionComparison.Less;

    [Tooltip("True: condition must hold against ALL matching entities. False: against ANY.")]
    public bool requireAll = true;

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        var targets = manager.FindEntitiesWithTags(targetTags);
        if (targets.Count == 0) return true;

        int draggablePos = axis == GridAxis.Vertical ? row : col;

        if (requireAll)
        {
            foreach (var t in targets)
            {
                int targetPos = axis == GridAxis.Vertical ? t.Row : t.Col;
                if (!ComparePosition(draggablePos, targetPos)) return false;
            }
            return true;
        }
        else
        {
            foreach (var t in targets)
            {
                int targetPos = axis == GridAxis.Vertical ? t.Row : t.Col;
                if (ComparePosition(draggablePos, targetPos)) return true;
            }
            return false;
        }
    }

    bool ComparePosition(int a, int b) => comparison switch
    {
        PositionComparison.Less           => a < b,
        PositionComparison.LessOrEqual    => a <= b,
        PositionComparison.Equal          => a == b,
        PositionComparison.GreaterOrEqual => a >= b,
        PositionComparison.Greater        => a > b,
        _                                 => false,
    };
}
