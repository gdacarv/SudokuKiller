using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Column")]
public class UniquePerColumnRule : DraggableRule
{
    public List<GridEntity.TagEntry> tags = new();

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        if (!draggable.Entity.MatchesAll(tags)) return true;
        return !manager.HasMatchInCol(col, tags, draggable);
    }
}
