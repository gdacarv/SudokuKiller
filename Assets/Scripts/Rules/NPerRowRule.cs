using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/N Per Row")]
public class NPerRowRule : Rule
{
    public List<GridEntity.TagEntry> tags = new();
    public int n = 1;
    public ComparisonMode comparison = ComparisonMode.LessThan;

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        if (!draggable.Entity.MatchesAll(tags)) return true;
        return Compare(manager.CountMatchesInRow(row, tags, draggable), comparison, n);
    }
}
