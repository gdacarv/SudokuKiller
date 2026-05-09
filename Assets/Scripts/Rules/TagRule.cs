using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Tag")]
public class TagRule : Rule
{
    public enum MatchMode { All, Any }
    public enum ExpectMode { Has, HasNot }

    public List<GridEntity.TagEntry> tags = new();
    public MatchMode matchMode = MatchMode.All;
    public ExpectMode expectMode = ExpectMode.Has;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        bool matches = matchMode == MatchMode.All
            ? target.Entity.MatchesAll(tags)
            : MatchesAny(target.Entity);

        return expectMode == ExpectMode.Has ? matches : !matches;
    }

    private bool MatchesAny(GridEntity entity)
    {
        foreach (var tag in tags)
            if (entity.HasTag(tag.key, tag.value)) return true;
        return false;
    }
}
