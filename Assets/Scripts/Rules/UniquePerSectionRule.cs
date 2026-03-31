using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Section")]
public class UniquePerSectionRule : DraggableRule
{
    public List<GridEntity.TagEntry> tags = new();

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        int section = manager.GetSection(row, col);
        if (section == -1) return true;
        if (!draggable.Entity.MatchesAll(tags)) return true;
        return !manager.HasMatchInSection(section, tags, draggable);
    }
}
