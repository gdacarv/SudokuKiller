using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Paints/erases markers on the grid while GridManager.enableUserBlockers is on.
/// Owned directly by GridManager (plain C# object, not a MonoBehaviour) so scenes need no extra wiring.
/// Two tools share the same click/drag/erase interaction: opaque blockers (default) that stop suspect
/// placement, and translucent drafts of whichever suspect currently sits in the DraftHolder (visual only).
/// </summary>
public class UserBlockerPainter
{
    private enum StrokeMode { None, Paint, Erase }

    private readonly struct Marker
    {
        public readonly GameObject go;
        public readonly bool isDraft;
        public Marker(GameObject go, bool isDraft) { this.go = go; this.isDraft = isDraft; }
    }

    private readonly GridManager _manager;
    private readonly DragInputProvider _inputProvider;
    private readonly Transform _root;
    private readonly Dictionary<Vector2Int, Marker> _markers = new();

    private static Sprite _fallbackSprite;

    private IdentifyKillerButton _identifyButton;
    private DraftHolder _draftHolder;
    private StrokeMode _stroke = StrokeMode.None;
    private Vector2Int? _lastCell;
    private bool _enabled;

    // Captured when a stroke starts, so a single drag always paints one consistent kind of marker.
    private bool _strokeIsDraft;
    private Draggable _strokeSource;

    private bool DraftMode => _draftHolder != null && _draftHolder.Held != null;

    public UserBlockerPainter(GridManager manager, DragInputProvider inputProvider)
    {
        _manager = manager;
        _inputProvider = inputProvider;

        var rootGo = new GameObject("_UserBlockers");
        rootGo.transform.SetParent(manager.transform, false);
        _root = rootGo.transform;
        _root.gameObject.SetActive(false);
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _root.gameObject.SetActive(enabled);
        EndStroke();

        if (enabled)
            PruneUnpaintableMarkers();
    }

    public void Tick()
    {
        if (!_enabled || _inputProvider == null) return;

        if (_identifyButton == null)
            _identifyButton = Object.FindFirstObjectByType<IdentifyKillerButton>();
        if (_draftHolder == null)
            _draftHolder = Object.FindFirstObjectByType<DraftHolder>();

        if (_identifyButton != null && _identifyButton.IsInIdentifyMode)
        {
            EndStroke();
            return;
        }

        if (_inputProvider.IsPressed)
        {
            BeginStroke();
        }
        else if (_stroke != StrokeMode.None)
        {
            if (_inputProvider.IsHeld)
                ContinueStroke();
            else
                EndStroke();
        }
    }

    private void BeginStroke()
    {
        if (IsPointerOverUI() || IsPointerOverClaimedCollider() || AnyDraggableDragging())
        {
            _stroke = StrokeMode.None;
            _lastCell = null;
            return;
        }

        Vector2Int? cell = CurrentCell();
        if (!cell.HasValue)
        {
            _stroke = StrokeMode.None;
            _lastCell = null;
            return;
        }

        if (_markers.ContainsKey(cell.Value))
        {
            _stroke = StrokeMode.Erase;
            RemoveMarker(cell.Value);
        }
        else
        {
            _strokeIsDraft = DraftMode;
            _strokeSource = _draftHolder != null ? _draftHolder.Held : null;

            if (CanPaint(cell.Value, _strokeIsDraft))
            {
                _stroke = StrokeMode.Paint;
                AddMarker(cell.Value);
            }
            else
            {
                _stroke = StrokeMode.None;
            }
        }

        _lastCell = _stroke != StrokeMode.None ? cell : (Vector2Int?)null;
    }

    private void ContinueStroke()
    {
        Vector2Int? cell = CurrentCell();
        if (!cell.HasValue)
        {
            // Leaving the grid breaks the stroke's line; it resumes fresh on re-entry.
            _lastCell = null;
            return;
        }

        if (_lastCell.HasValue)
        {
            foreach (var c in CellsOnLine(_lastCell.Value, cell.Value))
                ApplyStroke(c);
        }
        else
        {
            ApplyStroke(cell.Value);
        }

        _lastCell = cell;
    }

    private void ApplyStroke(Vector2Int cell)
    {
        if (_stroke == StrokeMode.Paint)
        {
            if (!_markers.ContainsKey(cell) && CanPaint(cell, _strokeIsDraft))
                AddMarker(cell);
        }
        else if (_stroke == StrokeMode.Erase)
        {
            if (_markers.ContainsKey(cell))
                RemoveMarker(cell);
        }
    }

    private void EndStroke()
    {
        _stroke = StrokeMode.None;
        _lastCell = null;
    }

    private bool CanPaint(Vector2Int cell, bool isDraft)
        => isDraft ? _manager.IsCellAvailable(cell.y, cell.x) : _manager.CanUserBlock(cell.y, cell.x);

    private Vector2Int? CurrentCell() => _manager.WorldToCell(_inputProvider.PointerWorldPosition);

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private bool IsPointerOverClaimedCollider()
    {
        var hits = Physics2D.OverlapPointAll(_inputProvider.PointerWorldPosition);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<Draggable>() != null) return true;
            if (hit.GetComponent<IdentifyKillerButton>() != null) return true;
            if (hit.GetComponent<DraftHolder>() != null) return true;
        }
        return false;
    }

    private static bool AnyDraggableDragging()
    {
        foreach (var d in Object.FindObjectsByType<Draggable>(FindObjectsSortMode.None))
            if (d.IsDragging) return true;
        return false;
    }

    // A marker's cell may no longer be paintable for its kind (became occupied, or a blocker
    // sprang up under a stale draft) after blockers were off for a while — never leave it stuck visible.
    private void PruneUnpaintableMarkers()
    {
        List<Vector2Int> toRemove = null;
        foreach (var kv in _markers)
            if (!CanPaint(kv.Key, kv.Value.isDraft))
                (toRemove ??= new List<Vector2Int>()).Add(kv.Key);

        if (toRemove == null) return;
        foreach (var cell in toRemove)
            RemoveMarker(cell);
    }

    private void AddMarker(Vector2Int cell)
    {
        if (_markers.ContainsKey(cell)) return;

        if (_strokeIsDraft)
        {
            _markers[cell] = new Marker(CreateDraftVisual(cell, _strokeSource), isDraft: true);
        }
        else
        {
            _manager.SetUserBlocked(cell.y, cell.x, true);
            _markers[cell] = new Marker(CreateBlockerVisual(cell), isDraft: false);
        }
    }

    private void RemoveMarker(Vector2Int cell)
    {
        if (!_markers.TryGetValue(cell, out var marker)) return;

        if (!marker.isDraft)
            _manager.SetUserBlocked(cell.y, cell.x, false);

        Object.Destroy(marker.go);
        _markers.Remove(cell);
    }

    // Removes whatever marker sits on a cell a suspect just got placed on — called by
    // GridManager.TryPlace regardless of whether blockers are currently enabled.
    public void ClearCell(int row, int col) => RemoveMarker(new Vector2Int(col, row));

    private GameObject CreateBlockerVisual(Vector2Int cell)
    {
        var overlay = _manager.gridOverlay;

        var go = new GameObject($"Blocker_{cell.y}_{cell.x}");
        go.transform.SetParent(_root, false);
        go.transform.position = _manager.GetCellCenter(cell.y, cell.x);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _manager.userBlockerSprite != null ? _manager.userBlockerSprite : GetFallbackSprite();
        sr.color = _manager.userBlockerColor;
        sr.sortingLayerName = overlay.sortingLayerName;
        sr.sortingOrder = _manager.userBlockerSortingOrder;

        Vector2 spriteSize = sr.sprite.bounds.size;
        float scaleX = spriteSize.x > 0f ? overlay.cellWidth / spriteSize.x : 1f;
        float scaleY = spriteSize.y > 0f ? overlay.cellHeight / spriteSize.y : 1f;
        go.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        return go;
    }

    // Translucent copy of the suspect currently held in the DraftHolder. Visual only —
    // never touches _blockedByUser, so it never affects placement.
    private GameObject CreateDraftVisual(Vector2Int cell, Draggable source)
    {
        var go = new GameObject($"Draft_{cell.y}_{cell.x}");
        go.transform.SetParent(_root, false);
        go.transform.position = _manager.GetCellCenter(cell.y, cell.x);

        var sr = go.AddComponent<SpriteRenderer>();

        var srcRenderer = source != null ? source.MainRenderer : null;
        if (srcRenderer != null)
        {
            sr.sprite = srcRenderer.sprite;
            sr.flipX = srcRenderer.flipX;
            sr.sortingLayerName = srcRenderer.sortingLayerName;
            sr.sortingOrder = srcRenderer.sortingOrder - 1;
            go.transform.localScale = source.transform.lossyScale;
        }

        sr.color = new Color(1f, 1f, 1f, _manager.draftMarkerAlpha);

        return go;
    }

    // Procedural red "X" used when no userBlockerSprite is assigned in the inspector.
    private static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null) return _fallbackSprite;

        const int size = 32;
        const float thickness = 0.08f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                bool onDiagonal = Mathf.Abs(u - v) < thickness || Mathf.Abs(u - (1f - v)) < thickness;
                pixels[y * size + x] = onDiagonal ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _fallbackSprite;
    }

    // Bresenham line, excluding 'from' (already applied by the caller) and including 'to'.
    private static IEnumerable<Vector2Int> CellsOnLine(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0), sx = x1 > x0 ? 1 : -1;
        int dy = Mathf.Abs(y1 - y0), sy = y1 > y0 ? 1 : -1;
        int err = dx - dy;
        int x = x0, y = y0;

        bool first = true;
        while (true)
        {
            if (!first) yield return new Vector2Int(x, y);
            first = false;

            if (x == x1 && y == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}
