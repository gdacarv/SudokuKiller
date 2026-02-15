using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Column")]
public class UniquePerColumnRule : DraggableRule
{
    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
        => !manager.HasTagInCol(col, draggable.tag, draggable);
}
