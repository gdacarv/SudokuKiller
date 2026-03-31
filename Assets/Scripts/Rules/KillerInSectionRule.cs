using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Killer Rules/Killer In Section")]
public class KillerInSectionRule : KillerRule
{
    public int targetSection;

    public override bool Evaluate(GridManager manager, Draggable suspect)
        => manager.GetSection(suspect.Entity.Row, suspect.Entity.Col) == targetSection;
}
