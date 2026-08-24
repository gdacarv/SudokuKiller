using System.Collections.Generic;

[UnityEngine.CreateAssetMenu(menuName = "Sudoku/Rules/Unique By Tag Key")]
public class UniqueByTagKeyRule : Rule
{
    [UnityEngine.Tooltip("Tag key that must be unique within the region (e.g. \"color\", \"name\").")]
    public string key = "color";

    public RegionScope scope = RegionScope.Row;

    [UnityEngine.Tooltip("Only entities matching all of these tags participate (AND logic). Empty = everyone with the key.")]
    public List<GridEntity.TagEntry> filterTags = new();

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        if (filterTags.Count > 0 && !target.Entity.MatchesAll(filterTags)) return true;

        string myValue = target.Entity.GetTag(key);
        if (myValue == null) return true; // no value on this entity — nothing to conflict with

        var occupants = manager.GetOccupantsInRegion(scope, row, col, target);
        foreach (var occ in occupants)
        {
            if (filterTags.Count > 0 && !occ.Entity.MatchesAll(filterTags)) continue;
            if (occ.Entity.GetTag(key) == myValue) return false;
        }
        return true;
    }
}
