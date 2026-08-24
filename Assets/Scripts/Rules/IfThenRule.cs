using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/If Then")]
public class IfThenRule : Rule
{
    [Tooltip("Antecedent. If this fails (or is unset), the rule is vacuously satisfied.")]
    public Rule condition;

    [Tooltip("Consequent. Only evaluated when 'condition' passes.")]
    public Rule consequence;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        if (condition == null || !condition.CanPlace(manager, target, row, col))
            return true; // vacuous truth

        return consequence == null || consequence.CanPlace(manager, target, row, col);
    }
}
