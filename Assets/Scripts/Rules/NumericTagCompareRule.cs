using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Numeric Tag Compare")]
public class NumericTagCompareRule : Rule
{
    [Tooltip("Tag key holding a numeric value on both this entity and each target (e.g. \"age\", \"value\").")]
    public string key = "age";

    [Tooltip("Tags identifying the target(s) to compare against (AND logic per entry).")]
    public List<GridEntity.TagEntry> targetTags = new();

    public PositionComparison comparison = PositionComparison.Greater;

    [Tooltip("True: comparison must hold against ALL matching targets. False: against ANY.")]
    public bool requireAll = true;

    [Tooltip("Result when no target has both a tag match and a parseable numeric value.")]
    public bool passWhenNoTargets = true;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        if (!target.Entity.TryGetNumericTag(key, out int myValue)) return passWhenNoTargets;

        var candidates = manager.FindEntitiesWithTags(targetTags).Where(t => t != target.Entity);
        var targets = new List<int>();
        foreach (var t in candidates)
            if (t.TryGetNumericTag(key, out int v))
                targets.Add(v);

        if (targets.Count == 0) return passWhenNoTargets;

        return requireAll
            ? targets.All(v => Compare(myValue, v))
            : targets.Any(v => Compare(myValue, v));
    }

    bool Compare(int a, int b) => comparison switch
    {
        PositionComparison.Less           => a <  b,
        PositionComparison.LessOrEqual    => a <= b,
        PositionComparison.Equal          => a == b,
        PositionComparison.GreaterOrEqual => a >= b,
        PositionComparison.Greater        => a >  b,
        _                                 => false,
    };
}
