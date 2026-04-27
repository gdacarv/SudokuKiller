using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
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

    SpriteRenderer _src;
    Collider2D _collider;
    SpriteRenderer[] _outlines;
    bool _active;

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
        _src = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        if (inputProvider == null)
            inputProvider = FindFirstObjectByType<DragInputProvider>();
        BuildOutlines();
    }

    void BuildOutlines()
    {
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;
        _outlines = new SpriteRenderer[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            var go = new GameObject("_Outline" + i);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            var mat = GetSilhouetteMaterial();
            if (mat != null) sr.material = mat;
            sr.enabled = false;
            _outlines[i] = sr;
        }
    }

    void Update()
    {
        bool hovered = false;
        if (inputProvider != null && !(suppressWhileDragging && inputProvider.IsHeld))
            hovered = _collider.OverlapPoint(inputProvider.PointerWorldPosition);
        _active = hovered || IsSelected;
    }

    void LateUpdate()
    {
        if (_src.sprite == null)
        {
            foreach (var o in _outlines) o.enabled = false;
            return;
        }

        float step = outlinePixels / _src.sprite.pixelsPerUnit;
        var dirs = outlineMode == Mode.FourDirections ? Dirs4 : Dirs8;

        for (int i = 0; i < _outlines.Length; i++)
        {
            var o = _outlines[i];
            o.enabled = _active;
            o.sprite = _src.sprite;
            o.flipX = _src.flipX;
            o.flipY = _src.flipY;
            o.sortingLayerID = _src.sortingLayerID;
            o.sortingOrder = _src.sortingOrder - 1;
            o.color = outlineColor;
            o.transform.localPosition = (Vector3)(dirs[i] * step);
            o.transform.localRotation = Quaternion.identity;
            o.transform.localScale = Vector3.one;
        }
    }

    void OnDestroy()
    {
        if (_outlines == null) return;
        foreach (var o in _outlines)
            if (o != null) Destroy(o.gameObject);
    }
}
