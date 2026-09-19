using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Matching Cell Tag")]
public class MatchingCellTagRule : Rule
{
    [Tooltip("Tags identifying the reference entity whose tile defines the 'kind' to match (e.g. role=Victim). AND logic per entry.")]
    public List<GridEntity.TagEntry> referenceTags = new();

    [Tooltip("Tag filter: only entities matching ALL of these count as a tile's object (e.g. type=object). Empty = every entity on the cell.")]
    public List<GridEntity.TagEntry> cellEntityTags = new();

    [Tooltip("Tag keys to compare. ALL listed keys must hold the same value on both objects. Empty = the objects' full tag sets must be identical.")]
    public List<string> compareKeys = new();

    [Tooltip("True: must match the tile of EVERY matching reference. False: of AT LEAST ONE.")]
    public bool requireAll = false;

    [Tooltip("Result for a reference whose tile carries no qualifying object at all (bare floor).")]
    public bool passWhenReferenceCellUntagged = false;

    public override bool CanPlace(GridManager manager, Draggable target, int row, int col)
    {
        // Hot path for the puzzle verifier: no LINQ, no per-call lists. The tagged lists may be shared
        // (see GridManager.GetTaggedEntities), so they are only read, and IsOnGrid is checked inline.
        var references = manager.GetTaggedEntities(referenceTags);
        var cellEntities = manager.GetTaggedEntities(cellEntityTags);

        bool sawReference = false;
        for (int i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            if (!reference.IsOnGrid || reference == target.Entity) continue;

            sawReference = true;
            bool passes = MatchesReferenceCell(cellEntities, reference, target.Entity, row, col);
            if (requireAll) { if (!passes) return false; }
            else if (passes) return true;
        }

        // No reference on the grid is a failure (deliberately not "vacuously true" — same convention as
        // DistanceToTagRule and the verifier's non-monotonicity note). requireAll survived every check;
        // any-mode found no passing reference.
        return sawReference && requireAll;
    }

    bool MatchesReferenceCell(List<GridEntity> cellEntities, GridEntity reference, GridEntity target, int row, int col)
    {
        bool referenceCellHasObject = false;
        for (int a = 0; a < cellEntities.Count; a++)
        {
            var objA = cellEntities[a];
            // 'reference' and 'target' are excluded by identity: each is the lone person on its own cell
            // (one occupant per cell), so a Person tag can never make two tiles trivially "the same kind".
            if (!IsObjectOn(objA, reference.Row, reference.Col, reference) || !Comparable(objA)) continue;

            referenceCellHasObject = true;
            for (int b = 0; b < cellEntities.Count; b++)
            {
                var objB = cellEntities[b];
                if (IsObjectOn(objB, row, col, target) && Comparable(objB) && SameKind(objA, objB))
                    return true;
            }
        }
        return !referenceCellHasObject && passWhenReferenceCellUntagged;
    }

    static bool IsObjectOn(GridEntity e, int row, int col, GridEntity excluded)
        => e != excluded && e.IsOnGrid && e.Row == row && e.Col == col;

    // An object with nothing to compare must not match another such object.
    bool Comparable(GridEntity e)
    {
        var tags = e.ResolvedTags;
        if (compareKeys.Count == 0) return tags.Count > 0;

        foreach (var key in compareKeys)
            if (e.HasKey(key)) return true;
        return false;
    }

    bool SameKind(GridEntity a, GridEntity b)
    {
        if (compareKeys.Count > 0)
        {
            foreach (var key in compareKeys)
                if (a.GetTag(key) != b.GetTag(key)) return false;
            return true;
        }

        // Full tag-set equality, in both directions, so an extra tag on either side breaks the match.
        foreach (var t in a.ResolvedTags)
            if (!b.HasTag(t.key, t.value)) return false;
        foreach (var t in b.ResolvedTags)
            if (!a.HasTag(t.key, t.value)) return false;
        return true;
    }
}
