using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Not")]
public class NotRule : Rule
{
    [Tooltip("The rule to negate. An empty slot is a common authoring slip (see GridManager.CheckBoardRules) — treated as no constraint, so CanPlace passes.")]
    public Rule inner;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
        => inner == null || !inner.CanPlace(manager, target, row, col);
}
