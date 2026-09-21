using System.Collections.Generic;
using UnityEngine;

public enum DistanceMetric
{
    [InspectorName("Manhattan  |dRow| + |dCol|  (walking distance)")]
    Manhattan,
    [InspectorName("Chebyshev  max(|dRow|, |dCol|)  (king-move, diagonals = 1)")]
    Chebyshev,
    [InspectorName("Euclidean  √(dRow²+dCol²)  (true geometric, non-integer)")]
    Euclidean,
}

[CreateAssetMenu(menuName = "Sudoku/Rules/Distance To Tag")]
public class DistanceToTagRule : Rule
{
    [Tooltip("Tags identifying the target object(s) (AND logic per entry — entity must match all entries).")]
    public List<GridEntity.TagEntry> targetTags = new();

    [Tooltip("Manhattan: |dRow|+|dCol| walking distance.\nChebyshev: max(|dRow|,|dCol|) king-move distance, diagonals count as 1.\nEuclidean: √(dRow²+dCol²) true geometric distance, may be non-integer.")]
    public DistanceMetric metric = DistanceMetric.Manhattan;
    public PositionComparison comparison = PositionComparison.LessOrEqual;

    [Tooltip("Distance threshold (in grid cells) compared against target↔target distance.")]
    public int distance = 2;

    [Tooltip("True: condition must hold against ALL matching entities. False: against ANY.")]
    public bool requireAll = false;

    [Tooltip("True: only consider target entities in the same section as the placed cell.")]
    public bool sameSectionOnly = false;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        // Hot path for the puzzle verifier: one pass, no LINQ, no per-call list. The tagged list may be
        // shared (see GridManager.GetTaggedEntities), so it is only read, and IsOnGrid is checked inline.
        var tagged = manager.GetTaggedEntities(targetTags);
        int section = sameSectionOnly ? manager.GetSection(row, col) : 0;

        for (int i = 0; i < tagged.Count; i++)
        {
            var t = tagged[i];
            if (!t.IsOnGrid || t == target.Entity) continue;
            if (sameSectionOnly && manager.GetSection(t.Row, t.Col) != section) continue;

            bool passes = CompareDistance(ComputeDistance(row, col, t));
            if (requireAll) { if (!passes) return false; }
            else if (passes) return true;
        }

        // Nothing failed or passed. requireAll ("far from ALL of them") is vacuously true when there is no
        // other target on the grid, so a lone suspect is not flagged for being too close to nobody. Any-mode
        // ("near ONE of them") still fails with no target: a missing object must not satisfy "next to X".
        // The verifier stays sound either way — it defers this rule until its dependencies are placed.
        return requireAll;
    }

    float ComputeDistance(int row, int col, GridEntity b)
    {
        int dr = Mathf.Abs(row - b.Row);
        int dc = Mathf.Abs(col - b.Col);
        return metric switch
        {
            DistanceMetric.Manhattan => dr + dc,
            DistanceMetric.Chebyshev => Mathf.Max(dr, dc),
            DistanceMetric.Euclidean => Mathf.Sqrt(dr * dr + dc * dc),
            _                        => dr + dc,
        };
    }

    bool CompareDistance(float d) => comparison switch
    {
        PositionComparison.Less           => d <  distance,
        PositionComparison.LessOrEqual    => d <= distance,
        PositionComparison.Equal          => Mathf.Approximately(d, distance),
        PositionComparison.GreaterOrEqual => d >= distance,
        PositionComparison.Greater        => d >  distance,
        _                                 => false,
    };
}
