using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
public class SpriteOutline : MonoBehaviour
{
    public enum Mode { FourDirections, EightDirections }

    [Header("Appearance")]
    public Color outlineColor = Color.white;
    [Min(1)] public int outlinePixels = 2;
    public Mode outlineMode = Mode.EightDirections;

    [Header("Behaviour")]
    public DragInputProvider inputProvider;
    public bool suppressWhileDragging = true;

    /// Set to true by external systems (e.g. controller selection) to force the outline visible.
    public bool IsSelected { get; set; }

    static readonly Vector2[] Dirs4 =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    static readonly Vector2[] Dirs8 =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2( 1,  1), new Vector2( 1, -1),
        new Vector2(-1,  1), new Vector2(-1, -1)
    };

    SpriteRenderer _spriteSrc;
    Tilemap _tilemap;
    TilemapRenderer _tilemapSrc;
    Collider2D _collider;

    // Sprite path
    SpriteRenderer[] _spriteOutlines;

    // Tilemap path
    GameObject[] _tilemapOutlineGOs;
    Tilemap[] _tilemapOutlineTMs;
    TilemapRenderer[] _tilemapOutlineTRs;
    float _pixelsPerUnit;

    bool _active;

    static readonly List<SpriteOutline> _instances = new();

    static Material _silhouetteMat;

    static Material GetSilhouetteMaterial()
    {
        if (_silhouetteMat != null) return _silhouetteMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _silhouetteMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return _silhouetteMat;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (inputProvider == null)
            inputProvider = FindFirstObjectByType<DragInputProvider>();
    }
#endif

    void Awake()
    {
        _spriteSrc = GetComponent<SpriteRenderer>();
        _tilemap = GetComponent<Tilemap>();
        _tilemapSrc = GetComponent<TilemapRenderer>();
        _collider = GetComponent<Collider2D>();
        if (inputProvider == null)
            inputProvider = FindFirstObjectByType<DragInputProvider>();

        if (_spriteSrc != null)
            BuildSpriteOutlines();
        else if (_tilemap != null && _tilemapSrc != null)
            BuildTilemapOutlines();
        else
        {
            Debug.LogWarning($"[SpriteOutline] {name}: requires SpriteRenderer or Tilemap+TilemapRenderer.", this);
            enabled = false;
        }
    }

    void BuildSpriteOutlines()
    {
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;
        _spriteOutlines = new SpriteRenderer[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            var go = new GameObject("_Outline" + i);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            var mat = GetSilhouetteMaterial();
            if (mat != null) sr.material = mat;
            sr.enabled = false;
            _spriteOutlines[i] = sr;
        }
    }

    void BuildTilemapOutlines()
    {
        _pixelsPerUnit = ResolveTilemapPPU();
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;
        var mat = GetSilhouetteMaterial();

        _tilemapOutlineGOs = new GameObject[dirs.Length];
        _tilemapOutlineTMs = new Tilemap[dirs.Length];
        _tilemapOutlineTRs = new TilemapRenderer[dirs.Length];

        for (int i = 0; i < dirs.Length; i++)
        {
            var go = new GameObject("_Outline" + i);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, worldPositionStays: false);

            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            if (mat != null) tr.material = mat;
            tr.sortingLayerID = _tilemapSrc.sortingLayerID;
            tr.sortingOrder = _tilemapSrc.sortingOrder - 1;
            tr.enabled = false;

            CopyTilesToChild(tm);

            _tilemapOutlineGOs[i] = go;
            _tilemapOutlineTMs[i] = tm;
            _tilemapOutlineTRs[i] = tr;
        }
    }

    void CopyTilesToChild(Tilemap dest)
    {
        dest.tileAnchor = _tilemap.tileAnchor;
        var bounds = _tilemap.cellBounds;
        dest.SetTilesBlock(bounds, _tilemap.GetTilesBlock(bounds));

        // Force white vertex color per cell so outlineColor from the shader is unmodified.
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (_tilemap.GetTile(pos) != null)
                dest.SetColor(pos, Color.white);
        }
    }

    float ResolveTilemapPPU()
    {
        foreach (var pos in _tilemap.cellBounds.allPositionsWithin)
        {
            var sprite = _tilemap.GetSprite(pos);
            if (sprite != null)
                return sprite.pixelsPerUnit;
        }
        return 100f;
    }

    /// Call after modifying source Tilemap tiles at runtime to re-sync outline children.
    public void RefreshTiles()
    {
        if (_tilemapOutlineTMs == null) return;
        _pixelsPerUnit = ResolveTilemapPPU();
        foreach (var tm in _tilemapOutlineTMs)
            CopyTilesToChild(tm);
    }

    void OnEnable()
    {
        _instances.Add(this);
    }

    void OnDisable()
    {
        _instances.Remove(this);
    }

    void Update()
    {
        bool hovered = false;
        if (inputProvider != null && !(suppressWhileDragging && inputProvider.IsHeld))
        {
            Vector3 pointerWorldPos = inputProvider.PointerWorldPosition;
            if (_collider.OverlapPoint(pointerWorldPos))
                hovered = IsTopmostHovered(pointerWorldPos);
        }
        _active = hovered || IsSelected;
    }

    bool IsTopmostHovered(Vector3 pointerWorldPos)
    {
        SpriteOutline topmost = null;
        int topmostLayerValue = int.MinValue;
        int topmostOrder = int.MinValue;
        int topmostID = int.MinValue;

        foreach (var instance in _instances)
        {
            if (instance._collider == null || !instance._collider.OverlapPoint(pointerWorldPos)) continue;

            instance.GetSortingKey(out int layerValue, out int order);
            int id = instance.GetInstanceID();

            if (layerValue > topmostLayerValue ||
                (layerValue == topmostLayerValue && order > topmostOrder) ||
                (layerValue == topmostLayerValue && order == topmostOrder && id > topmostID))
            {
                topmost = instance;
                topmostLayerValue = layerValue;
                topmostOrder = order;
                topmostID = id;
            }
        }

        return topmost == this;
    }

    void GetSortingKey(out int layerValue, out int order)
    {
        if (_spriteSrc != null)
        {
            layerValue = SortingLayer.GetLayerValueFromID(_spriteSrc.sortingLayerID);
            order = _spriteSrc.sortingOrder;
        }
        else if (_tilemapSrc != null)
        {
            layerValue = SortingLayer.GetLayerValueFromID(_tilemapSrc.sortingLayerID);
            order = _tilemapSrc.sortingOrder;
        }
        else
        {
            layerValue = int.MinValue;
            order = int.MinValue;
        }
    }

    void LateUpdate()
    {
        if (_spriteOutlines != null)
            LateUpdateSprite();
        else if (_tilemapOutlineTRs != null)
            LateUpdateTilemap();
    }

    void LateUpdateSprite()
    {
        if (_spriteSrc.sprite == null)
        {
            foreach (var o in _spriteOutlines) o.enabled = false;
            return;
        }

        float step = outlinePixels / _spriteSrc.sprite.pixelsPerUnit;
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;

        for (int i = 0; i < _spriteOutlines.Length; i++)
        {
            var o = _spriteOutlines[i];
            o.enabled = _active;
            o.sprite = _spriteSrc.sprite;
            o.flipX = _spriteSrc.flipX;
            o.flipY = _spriteSrc.flipY;
            o.sortingLayerID = _spriteSrc.sortingLayerID;
            o.sortingOrder = _spriteSrc.sortingOrder - 1;
            o.color = outlineColor;
            o.transform.localPosition = (Vector3)(dirs[i] * step);
            o.transform.localRotation = Quaternion.identity;
            o.transform.localScale = Vector3.one;
        }
    }

    void LateUpdateTilemap()
    {
        float step = outlinePixels / _pixelsPerUnit;
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;

        for (int i = 0; i < _tilemapOutlineTRs.Length; i++)
        {
            var tr = _tilemapOutlineTRs[i];
            tr.enabled = _active;
            tr.sortingLayerID = _tilemapSrc.sortingLayerID;
            tr.sortingOrder = _tilemapSrc.sortingOrder - 1;
            _tilemapOutlineTMs[i].color = outlineColor;
            _tilemapOutlineGOs[i].transform.localPosition = (Vector3)(dirs[i] * step);
            _tilemapOutlineGOs[i].transform.localRotation = Quaternion.identity;
            _tilemapOutlineGOs[i].transform.localScale = Vector3.one;
        }
    }

    void OnDestroy()
    {
        if (_spriteOutlines != null)
            foreach (var o in _spriteOutlines)
                if (o != null) Destroy(o.gameObject);

        if (_tilemapOutlineGOs != null)
            foreach (var go in _tilemapOutlineGOs)
                if (go != null) Destroy(go);
    }
}
