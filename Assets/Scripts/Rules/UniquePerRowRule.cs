using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Row")]
public class UniquePerRowRule : DraggableRule
{
    public List<GridEntity.TagEntry> tags = new();

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        if (!draggable.Entity.MatchesAll(tags)) return true;
        return !manager.HasMatchInRow(row, tags, draggable);
    }
}
