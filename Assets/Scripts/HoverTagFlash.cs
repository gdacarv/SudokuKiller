using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Attach to a suspect. After the pointer rests on it for hoverDelay seconds (using SpriteOutline's
/// topmost-hover rules), every entity whose tags match any of the listed tag sets flashes its outline.
/// Repeats every hoverDelay while the hover lasts.
[RequireComponent(typeof(SpriteOutline))]
public class HoverTagFlash : MonoBehaviour
{
    [Serializable]
    public class TagSet
    {
        public List<GridEntity.TagEntry> tags = new();
    }

    [Header("Targets")]
    [Tooltip("An entity flashes if it has ALL tags of ANY one of these sets.")]
    public List<TagSet> tagSets = new();

    [Header("Timing")]
    [Min(0f)] public float hoverDelay = 3f;
    [Min(1)] public int flashCount = 3;
    [Min(0.01f)] public float flashOnDuration = 0.15f;
    [Min(0.01f)] public float flashOffDuration = 0.15f;

    [Header("Appearance")]
    public bool overrideOutlineColor = true;
    public Color flashColor = Color.yellow;

    SpriteOutline[] _outlines;
    GridManager _gridManager;
    float _hoverTimer;
    Coroutine _flashRoutine;
    readonly List<SpriteOutline> _targets = new();

    void Awake()
    {
        // All outlines on this object: if duplicates exist they tie on sort order and only one ever wins
        // SpriteOutline's topmost-hover test, so hover must be read from any of them.
        CacheOutlines();
        _gridManager = FindFirstObjectByType<GridManager>();
    }

    void CacheOutlines() => _outlines = GetComponents<SpriteOutline>();

    bool IsHovered()
    {
        if (_outlines == null || _outlines.Length == 0) CacheOutlines(); // Awake doesn't re-run after a script hot-reload in play mode
        foreach (var o in _outlines)
            if (o != null && o.IsHovered) return true;
        return false;
    }

    bool IsOwnOutline(SpriteOutline outline)
    {
        if (_outlines == null || _outlines.Length == 0) CacheOutlines();
        foreach (var o in _outlines)
            if (o == outline) return true;
        return false;
    }

    void Update()
    {
        bool enabledByManager = _gridManager == null || _gridManager.enableHoverTagFlash;
        if (!enabledByManager)
            StopFlash();

        if (!enabledByManager || !IsHovered())
        {
            _hoverTimer = 0f;
            return;
        }

        _hoverTimer += Time.deltaTime;
        if (_hoverTimer >= hoverDelay)
        {
            _hoverTimer = 0f;
            TriggerFlash();
        }
    }

    void TriggerFlash()
    {
        StopFlash();
        CollectTargets();
        if (_targets.Count == 0) return;
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    void CollectTargets()
    {
        _targets.Clear();
        var seen = new HashSet<SpriteOutline>();
        foreach (var entity in FindObjectsByType<GridEntity>(FindObjectsSortMode.None))
        {
            if (!MatchesAnySet(entity)) continue;

            var outline = entity.GetComponentInParent<SpriteOutline>();
            if (outline != null)
            {
                AddTarget(outline, seen);
                continue;
            }

            // Outline may live on a child (e.g. a visual child under the tagged root). Only take outlines
            // whose nearest GridEntity is this one, so a container doesn't flash its unrelated contents.
            foreach (var child in entity.GetComponentsInChildren<SpriteOutline>(false))
                if (child.GetComponentInParent<GridEntity>() == entity)
                    AddTarget(child, seen);
        }
    }

    void AddTarget(SpriteOutline outline, HashSet<SpriteOutline> seen)
    {
        if (!outline.enabled || IsOwnOutline(outline)) return;
        if (seen.Add(outline)) _targets.Add(outline);
    }

    bool MatchesAnySet(GridEntity entity)
    {
        foreach (var set in tagSets)
            if (set != null && set.tags.Count > 0 && entity.MatchesAll(set.tags))
                return true;
        return false;
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            SetFlashing(true);
            yield return new WaitForSeconds(flashOnDuration);
            SetFlashing(false);
            yield return new WaitForSeconds(flashOffDuration);
        }
        ClearTargets();
        _flashRoutine = null;
    }

    void SetFlashing(bool on)
    {
        foreach (var t in _targets)
        {
            if (t == null) continue;
            t.FlashColorOverride = on && overrideOutlineColor ? flashColor : (Color?)null;
            t.IsFlashing = on;
        }
    }

    void ClearTargets()
    {
        SetFlashing(false);
        _targets.Clear();
    }

    void StopFlash()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        ClearTargets();
    }

    void OnDisable()
    {
        _hoverTimer = 0f;
        StopFlash();
    }
}
