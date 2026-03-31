using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Killer Rules/Killer Tag")]
public class KillerTagRule : KillerRule
{
    public enum MatchMode { All, Any }
    public enum ExpectMode { Has, HasNot }

    public List<GridEntity.TagEntry> tags = new();
    public MatchMode matchMode = MatchMode.All;
    public ExpectMode expectMode = ExpectMode.Has;

    public override bool Evaluate(GridManager manager, Draggable suspect)
    {
        bool matches = matchMode == MatchMode.All
            ? suspect.Entity.MatchesAll(tags)
            : MatchesAny(suspect.Entity);

        return expectMode == ExpectMode.Has ? matches : !matches;
    }

    private bool MatchesAny(GridEntity entity)
    {
        foreach (var tag in tags)
            if (entity.HasTag(tag.key, tag.value)) return true;
        return false;
    }
}
