using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Count Of")]
public class CountOfRule : Rule
{
    [Tooltip("Child rules to evaluate. Null entries are skipped (never count as passing).")]
    public List<Rule> options = new();

    public int n = 1;
    public ComparisonMode comparison = ComparisonMode.EqualTo;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        int count = options.Count(r => r != null && r.CanPlace(manager, target, row, col));
        return Compare(count, comparison, n);
    }
}
