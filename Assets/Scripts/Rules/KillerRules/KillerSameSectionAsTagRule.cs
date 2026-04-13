using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Killer Rules/Killer Same Section As Tag")]
public class KillerSameSectionAsTagRule : KillerRule
{
    public List<GridEntity.TagEntry> targetTags = new();

    public override bool Evaluate(GridManager manager, Draggable suspect)
    {
        int suspectSection = manager.GetSection(suspect.Entity.Row, suspect.Entity.Col);
        if (suspectSection == -1) return false;

        var targets = manager.FindEntitiesWithTags(targetTags);
        foreach (var target in targets)
        {
            if (manager.GetSection(target.Row, target.Col) == suspectSection)
                return true;
        }
        return false;
    }
}
