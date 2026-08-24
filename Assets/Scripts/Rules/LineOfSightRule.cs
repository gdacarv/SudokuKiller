using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Line Of Sight")]
public class LineOfSightRule : Rule
{
    [Tooltip("Tags identifying who/what I must (or must not, with Negated) see (AND logic per entry).")]
    public List<GridEntity.TagEntry> targetTags = new();

    [Tooltip("Tags identifying entities that break a line of sight (e.g. blocksSight=true on walls/furniture).")]
    public List<GridEntity.TagEntry> blockerTags = new();

    [Tooltip("Also allow diagonal lines of sight, not just same row/column.")]
    public bool includeDiagonals = false;

    [Tooltip("True: line of sight must hold to ALL matching targets. False: to ANY.")]
    public bool requireAll = false;

    [Tooltip("Invert the result — e.g. 'nobody could see the pantry'.")]
    public bool negated = false;

    [Tooltip("Result when no entity matches targetTags.")]
    public bool passWhenNoTargets = true;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        var targets = manager.FindEntitiesWithTags(targetTags).Where(t => t != target.Entity).ToList();
        if (targets.Count == 0) return passWhenNoTargets;

        var blockers = manager.FindEntitiesWithTags(blockerTags).Where(b => b != target.Entity).ToList();

        bool result = requireAll
            ? targets.All(t => HasLineOfSight(row, col, t, blockers))
            : targets.Any(t => HasLineOfSight(row, col, t, blockers));

        return negated ? !result : result;
    }

    bool HasLineOfSight(int row, int col, GridEntity t, List<GridEntity> blockers)
    {
        int dr = t.Row - row;
        int dc = t.Col - col;
        if (dr == 0 && dc == 0) return true;

        bool sameRow = dr == 0;
        bool sameCol = dc == 0;
        bool sameDiag = includeDiagonals && Mathf.Abs(dr) == Mathf.Abs(dc);
        if (!sameRow && !sameCol && !sameDiag) return false;

        int stepR = Math.Sign(dr);
        int stepC = Math.Sign(dc);
        int steps = Mathf.Max(Mathf.Abs(dr), Mathf.Abs(dc));

        for (int i = 1; i < steps; i++)
        {
            int r = row + stepR * i;
            int c = col + stepC * i;
            foreach (var b in blockers)
                if (b.Row == r && b.Col == c) return false;
        }
        return true;
    }
}
