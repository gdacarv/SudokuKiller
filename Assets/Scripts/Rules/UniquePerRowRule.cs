using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Row")]
public class UniquePerRowRule : DraggableRule
{
    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
        => !manager.HasTagInRow(row, draggable.tag, draggable);
}
