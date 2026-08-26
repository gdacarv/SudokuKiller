using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Section Sum")]
public class RegionSumRule : Rule
{
    [Tooltip("Tag key holding a numeric value on each summed occupant (e.g. \"age\", \"value\").")]
    public string valueKey = "value";

    public RegionScope scope = RegionScope.Section;

    [Tooltip("Only sum occupants matching all of these tags (AND logic). Empty = sum everyone with a parseable value.")]
    public List<GridEntity.TagEntry> filterTags = new();

    public ComparisonMode comparison = ComparisonMode.EqualTo;
    public int total;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        // Unsectioned cells have no meaningful region to sum — matches NPerSectionRule's convention.
        if (scope == RegionScope.Section && manager.GetSection(row, col) == -1) return true;

        var occupants = manager.GetOccupantsInRegion(scope, row, col, target);
        int sum = 0;
        foreach (var occ in occupants)
        {
            if (filterTags.Count > 0 && !occ.Entity.MatchesAll(filterTags)) continue;
            if (occ.Entity.TryGetNumericTag(valueKey, out int v)) sum += v;
        }

        // GetOccupantsInRegion excludes 'target' itself (it may not be placed in _occupants yet),
        // so fold its own value in manually using the (row, col) this CanPlace call is testing.
        if ((filterTags.Count == 0 || target.Entity.MatchesAll(filterTags))
            && target.Entity.TryGetNumericTag(valueKey, out int myValue))
            sum += myValue;

        return Compare(sum, comparison, total);
    }
}
