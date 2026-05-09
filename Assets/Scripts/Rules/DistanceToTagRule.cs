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

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        var targets = manager.FindEntitiesWithTags(targetTags);
        if (targets.Count == 0) return false;

        if (requireAll)
        {
            foreach (var t in targets)
                if (!CompareDistance(ComputeDistance(row, col, t))) return false;
            return true;
        }
        else
        {
            foreach (var t in targets)
                if (CompareDistance(ComputeDistance(row, col, t))) return true;
            return false;
        }
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
