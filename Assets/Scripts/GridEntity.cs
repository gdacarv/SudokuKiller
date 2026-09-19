using System;
using System.Collections.Generic;
using UnityEngine;

public class GridEntity : MonoBehaviour
{
    [Serializable]
    public struct TagEntry
    {
        public string key;
        public string value;
    }

    public List<TagEntry> tags = new();

    [Tooltip("When enabled, this entity ignores its own tags and uses the tags of the nearest ancestor GridEntity instead.")]
    public bool inheritTagsFromParent;

    [HideInInspector] public int Row = -1;
    [HideInInspector] public int Col = -1;

    public bool IsOnGrid => Row >= 0 && Col >= 0;

    static readonly List<TagEntry> _empty = new();

    // ParentEntity is a GetComponentInParent walk that sits inside the innermost tag-matching loop.
    // Memoizing it is only safe while the hierarchy is frozen, so it is scoped to a window opened by
    // GridManager.BeginTagQueryCache (e.g. a verifier search) and dropped when the window closes.
    static bool s_parentCacheEnabled;
    static int s_parentCacheEpoch;
    GridEntity _cachedParent;
    int _cachedParentEpoch = -1;

    public static void SetParentCacheEnabled(bool enabled)
    {
        s_parentCacheEnabled = enabled;
        s_parentCacheEpoch++; // invalidates every entity's memo, so a later window never sees an old one
    }

    public GridEntity ParentEntity
    {
        get
        {
            if (!s_parentCacheEnabled)
                return transform.parent != null ? transform.parent.GetComponentInParent<GridEntity>() : null;

            if (_cachedParentEpoch != s_parentCacheEpoch)
            {
                _cachedParent = transform.parent != null ? transform.parent.GetComponentInParent<GridEntity>() : null;
                _cachedParentEpoch = s_parentCacheEpoch;
            }
            return _cachedParent;
        }
    }

    public List<TagEntry> ResolvedTags
    {
        get
        {
            if (inheritTagsFromParent)
            {
                var parent = ParentEntity;
                return parent != null ? parent.ResolvedTags : _empty;
            }
            return tags;
        }
    }

    public string GetTag(string key)
    {
        foreach (var entry in ResolvedTags)
            if (entry.key == key)
                return entry.value;
        return null;
    }

    public bool HasTag(string key, string value)
    {
        foreach (var entry in ResolvedTags)
            if (entry.key == key && entry.value == value)
                return true;
        return false;
    }

    public bool HasKey(string key)
    {
        foreach (var entry in ResolvedTags)
            if (entry.key == key)
                return true;
        return false;
    }

    public bool MatchesAll(List<TagEntry> pattern)
    {
        foreach (var p in pattern)
            if (!HasTag(p.key, p.value))
                return false;
        return true;
    }

    /// <summary>Parses the tag value at 'key' as an int (e.g. "age", "value"). Returns false if the key is absent or not numeric.</summary>
    public bool TryGetNumericTag(string key, out int value)
    {
        value = 0;
        var raw = GetTag(key);
        return raw != null && int.TryParse(raw, out value);
    }
}
