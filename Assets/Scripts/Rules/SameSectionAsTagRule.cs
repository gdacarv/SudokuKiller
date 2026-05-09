using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Same Section As Tag")]
public class SameSectionAsTagRule : Rule
{
    public List<GridEntity.TagEntry> targetTags = new();

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        int section = manager.GetSection(row, col);
        if (section == -1) return false;

        var targets = manager.FindEntitiesWithTags(targetTags);
        foreach (var t in targets)
            if (manager.GetSection(t.Row, t.Col) == section) return true;
        return false;
    }
}
