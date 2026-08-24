using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/As Tagged Entity")]
public class AsTaggedEntityRule : Rule
{
    [Tooltip("Tags identifying the subject(s) to evaluate 'inner' against, in place of whoever this rule is attached to (AND logic per entry).")]
    public List<GridEntity.TagEntry> subjectTags = new();

    [Tooltip("Rule to evaluate as if each subject were the one being placed.")]
    public Rule inner;

    [Tooltip("True: 'inner' must hold for EVERY matching subject (universal). False: for AT LEAST ONE (existential).")]
    public bool requireAll = false;

    [Tooltip("Result when no entity matches subjectTags, or none of the matches have a Draggable to evaluate 'inner' against (e.g. they're static scenery).")]
    public bool passWhenNoSubject = true;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        if (inner == null) return true;

        // Only entities with a Draggable can stand in as 'self' for 'inner' — tag-matched scenery is skipped.
        var subjects = manager.FindEntitiesWithTags(subjectTags)
            .Select(e => (entity: e, draggable: e.GetComponent<Draggable>()))
            .Where(s => s.draggable != null)
            .ToList();
        if (subjects.Count == 0) return passWhenNoSubject;

        return requireAll
            ? subjects.All(s => inner.CanPlace(manager, s.draggable, s.entity.Row, s.entity.Col))
            : subjects.Any(s => inner.CanPlace(manager, s.draggable, s.entity.Row, s.entity.Col));
    }
}
