using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/In Section")]
public class InSectionRule : Rule
{
    public int targetSection;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
        => manager.GetSection(row, col) == targetSection;
}
