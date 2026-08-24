using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Directional Offset")]
public class DirectionalOffsetRule : Rule
{
    // Grid convention (see GridManager.GetCellCenter/WorldToCell): row increases upward, col increases rightward.
    public enum Dir { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight }

    static readonly Dictionary<Dir, (int dr, int dc)> Vectors = new()
    {
        { Dir.Up,        ( 1,  0) }, { Dir.Down,      (-1,  0) },
        { Dir.Right,      (0,  1) }, { Dir.Left,       (0, -1) },
        { Dir.UpRight,   ( 1,  1) }, { Dir.UpLeft,    ( 1, -1) },
        { Dir.DownRight, (-1,  1) }, { Dir.DownLeft,  (-1, -1) },
    };

    [Tooltip("Tags identifying the target(s) I am positioned relative to (AND logic per entry).")]
    public List<GridEntity.TagEntry> targetTags = new();

    [Tooltip("My direction relative to the target, e.g. Left = 'I am to the left of the target'.")]
    public Dir direction = Dir.Right;

    [Tooltip("0 = any distance in that direction. >0 = exact cell offset along each relevant axis.")]
    public int exactDistance = 1;

    [Tooltip("True: offset must hold against ALL matching targets. False: against ANY.")]
    public bool requireAll = false;

    public bool negated = false;

    [Tooltip("Result when no entity matches targetTags.")]
    public bool passWhenNoTargets = true;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        var targets = manager.FindEntitiesWithTags(targetTags).Where(t => t != target.Entity).ToList();
        if (targets.Count == 0) return passWhenNoTargets;

        bool result = requireAll
            ? targets.All(t => MatchesOffset(row, col, t))
            : targets.Any(t => MatchesOffset(row, col, t));

        return negated ? !result : result;
    }

    bool MatchesOffset(int row, int col, GridEntity t)
    {
        var (ur, uc) = Vectors[direction];
        int deltaR = row - t.Row;
        int deltaC = col - t.Col;

        // Axes not part of this direction must stay exactly aligned.
        if (ur == 0 && deltaR != 0) return false;
        if (uc == 0 && deltaC != 0) return false;

        if (ur != 0)
        {
            if (Math.Sign(deltaR) != ur) return false;
            if (exactDistance > 0 && Mathf.Abs(deltaR) != exactDistance) return false;
        }
        if (uc != 0)
        {
            if (Math.Sign(deltaC) != uc) return false;
            if (exactDistance > 0 && Mathf.Abs(deltaC) != exactDistance) return false;
        }
        return true;
    }
}
