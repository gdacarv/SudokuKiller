using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Any Of")]
public class AnyOfRule : Rule
{
    [Tooltip("Passes if ANY of these child rules pass. An empty list never passes.")]
    public List<Rule> options = new();

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
        => options.Any(r => r != null && r.CanPlace(manager, target, row, col));
}
