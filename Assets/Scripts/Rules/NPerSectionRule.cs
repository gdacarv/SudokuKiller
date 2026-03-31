using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/N Per Section")]
public class NPerSectionRule : DraggableRule
{
    public List<GridEntity.TagEntry> tags = new();
    public int n = 1;
    public ComparisonMode comparison = ComparisonMode.LessThan;

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        int section = manager.GetSection(row, col);
        if (section == -1) return true;
        if (!draggable.Entity.MatchesAll(tags)) return true;
        return Compare(manager.CountMatchesInSection(section, tags, draggable), comparison, n);
    }
}
