using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Between")]
public class BetweenRule : Rule
{
    public enum BetweenMode
    {
        [InspectorName("Same Line  (strict: A, me, B collinear on one row/column)")]
        SameLine,
        [InspectorName("Axis Projection  (soft: between A and B along one axis only)")]
        AxisProjection,
    }

    [Tooltip("Tags identifying endpoint A (AND logic per entry).")]
    public List<GridEntity.TagEntry> endpointATags = new();

    [Tooltip("Tags identifying endpoint B (AND logic per entry).")]
    public List<GridEntity.TagEntry> endpointBTags = new();

    public BetweenMode mode = BetweenMode.SameLine;

    [Tooltip("Axis used by Axis Projection mode.")]
    public GridAxis axis = GridAxis.Horizontal;

    [Tooltip("Invert the result.")]
    public bool negated = false;

    [Tooltip("Result when either endpoint has no matching entity.")]
    public bool passWhenNoTargets = true;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        var aList = manager.FindEntitiesWithTags(endpointATags).Where(e => e != target.Entity).ToList();
        var bList = manager.FindEntitiesWithTags(endpointBTags).Where(e => e != target.Entity).ToList();
        if (aList.Count == 0 || bList.Count == 0) return passWhenNoTargets;

        bool result = false;
        foreach (var a in aList)
        {
            foreach (var b in bList)
            {
                if (a == b) continue;
                if (IsBetween(row, col, a, b)) { result = true; break; }
            }
            if (result) break;
        }

        return negated ? !result : result;
    }

    bool IsBetween(int row, int col, GridEntity a, GridEntity b)
    {
        if (mode == BetweenMode.AxisProjection)
        {
            int aPos = axis == GridAxis.Horizontal ? a.Col : a.Row;
            int bPos = axis == GridAxis.Horizontal ? b.Col : b.Row;
            int myPos = axis == GridAxis.Horizontal ? col : row;
            int lo = Mathf.Min(aPos, bPos), hi = Mathf.Max(aPos, bPos);
            return myPos > lo && myPos < hi;
        }

        // SameLine: A, target and B must all sit on the same row or the same column.
        if (a.Row == b.Row && a.Row == row)
        {
            int lo = Mathf.Min(a.Col, b.Col), hi = Mathf.Max(a.Col, b.Col);
            return col > lo && col < hi;
        }
        if (a.Col == b.Col && a.Col == col)
        {
            int lo = Mathf.Min(a.Row, b.Row), hi = Mathf.Max(a.Row, b.Row);
            return row > lo && row < hi;
        }
        return false;
    }
}
