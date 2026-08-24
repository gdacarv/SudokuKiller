using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Neighbor Count")]
public class NeighborCountRule : Rule
{
    [Tooltip("Tags identifying entities that count as neighbors (AND logic per entry).")]
    public List<GridEntity.TagEntry> neighborTags = new();

    public DistanceMetric metric = DistanceMetric.Chebyshev;

    [Tooltip("Neighbors at this distance or closer are counted.")]
    public int radius = 1;

    public ComparisonMode comparison = ComparisonMode.EqualTo;
    public int n = 0;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        var candidates = manager.FindEntitiesWithTags(neighborTags).Where(t => t != target.Entity);
        int count = candidates.Count(t => ComputeDistance(row, col, t) <= radius);
        return Compare(count, comparison, n);
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
}
