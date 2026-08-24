using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/All Of")]
public class AllOfRule : Rule
{
    [Tooltip("Passes if ALL of these child rules pass. An empty list is vacuously true. A null entry is a common authoring slip — treated as no constraint.")]
    public List<Rule> options = new();

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
        => options.All(r => r == null || r.CanPlace(manager, target, row, col));
}
