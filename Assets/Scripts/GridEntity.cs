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

    [HideInInspector] public int Row = -1;
    [HideInInspector] public int Col = -1;

    public bool IsOnGrid => Row >= 0 && Col >= 0;

    public string GetTag(string key)
    {
        foreach (var entry in tags)
            if (entry.key == key)
                return entry.value;
        return null;
    }

    public bool HasTag(string key, string value)
    {
        foreach (var entry in tags)
            if (entry.key == key && entry.value == value)
                return true;
        return false;
    }

    public bool HasKey(string key)
    {
        foreach (var entry in tags)
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
}
