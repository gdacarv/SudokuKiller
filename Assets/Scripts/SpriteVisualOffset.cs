using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Shifts only the rendered sprite by a per-object amount. The transform, colliders and
/// children are untouched, so grid snapping and hit-testing keep using the real position.
/// Attach to any GameObject with a SpriteRenderer and tune Offset in the Inspector; it
/// updates live in edit mode. Works by cloning the assigned sprite with a shifted pivot.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteVisualOffset : MonoBehaviour
{
    [Tooltip("Visual offset in world units. +X moves the art right, +Y moves it up.")]
    [SerializeField] private Vector2 offset;

    // The authored sprite. Kept here because the shifted clone is a runtime-only object
    // that must never be saved into the scene/prefab.
    [SerializeField, HideInInspector] private Sprite sourceSprite;

    private SpriteRenderer _sr;
    private Sprite _shifted;

    public Vector2 Offset
    {
        get => offset;
        set
        {
            offset = value;
            Apply();
        }
    }

    void OnEnable()
    {
        Apply();
#if UNITY_EDITOR
        WarnIfAnimatorPresent();
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        PrefabStage.prefabSaving += OnPrefabSaving;
        PrefabStage.prefabSaved += OnPrefabSaved;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        PrefabStage.prefabSaved -= OnPrefabSaved;
#endif
        Restore();
    }

    void OnDestroy()
    {
        Restore();
    }

    void LateUpdate()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && _sr.sprite != _shifted)
            Apply();
    }

#if UNITY_EDITOR
    private bool _warnedAnimatorConflict;

    void OnValidate()
    {
        EditorApplication.delayCall += () =>
        {
            if (this != null) Apply();
        };
        WarnIfAnimatorPresent();
    }

    private void WarnIfAnimatorPresent()
    {
        if (_warnedAnimatorConflict) return;
        if (GetComponent<Animator>() == null) return;

        Debug.LogWarning(
            $"[SpriteVisualOffset] '{name}' also has an Animator. This component rebuilds a " +
            "shifted sprite clone every time the rendered sprite changes, so a sprite-swap " +
            "animation here will allocate a new clone every frame. Prefer the 'Create Offset " +
            "Child' tool (Tools/Sprite Visual Offset/Create Offset Child...) for animated objects.",
            this);
        _warnedAnimatorConflict = true;
    }

    private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => Restore();
    private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene) => Apply();
    private void OnPrefabSaving(GameObject prefab) => Restore();
    private void OnPrefabSaved(GameObject prefab) => Apply();
#endif

    /// <summary>Rebuilds the offset sprite (if needed) and assigns it to the renderer.</summary>
    private void Apply()
    {
        if (this == null) return;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) return;

        // Pick up a sprite the user just assigned in the Inspector (i.e. anything that
        // isn't our own generated clone) as the new source.
        if (_sr.sprite != null && _sr.sprite != _shifted)
            sourceSprite = _sr.sprite;

        DestroyShifted();

        if (sourceSprite == null)
            return;

        if (offset == Vector2.zero)
        {
            _sr.sprite = sourceSprite;
            return;
        }

        _shifted = BuildShiftedSprite(sourceSprite, offset);
        _sr.sprite = _shifted;
    }

    /// <summary>Restores the authored sprite and discards the generated clone.</summary>
    private void Restore()
    {
        DestroyShifted();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && sourceSprite != null)
            _sr.sprite = sourceSprite;
    }

    private void DestroyShifted()
    {
        if (_shifted == null) return;

        if (Application.isPlaying)
            Destroy(_shifted);
        else
            DestroyImmediate(_shifted);

        _shifted = null;
    }

    private static Sprite BuildShiftedSprite(Sprite src, Vector2 worldOffset)
    {
        Rect rect = src.rect;
        float ppu = src.pixelsPerUnit;

        // Moving the pivot opposite the desired offset makes the art render shifted
        // by +offset while the transform itself never moves.
        Vector2 pivotPx = src.pivot - worldOffset * ppu;
        var pivotNorm = new Vector2(pivotPx.x / rect.width, pivotPx.y / rect.height);

        var shifted = Sprite.Create(src.texture, rect, pivotNorm, ppu, 0, SpriteMeshType.FullRect, src.border);
        shifted.name = src.name + " (offset)";
        shifted.hideFlags = HideFlags.HideAndDontSave;
        return shifted;
    }
}
