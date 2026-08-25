using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Sprite Visual Offset/Create Offset Child... — moves the selected GameObject's
/// SpriteRenderer onto a new "Visual" child positioned at the given local offset, so the
/// art can be nudged without moving the parent's transform/collider. Prefer this over
/// SpriteVisualOffset for objects with an Animator driving sprite-swap animation, since it
/// doesn't rebuild a sprite clone every frame.
///
/// While the window is open, dragging the Offset field live-previews the result in the
/// Scene view via a temporary, non-persistent child. Nothing is written to the scene until
/// "Apply" is pressed. "Move existing colliders too" (on by default) shifts any
/// Box/Circle/Capsule/Polygon Collider2D on the object by the same offset, so hit-testing
/// (e.g. Draggable's click detection) still lines up with the shifted art.
/// </summary>
public class CreateOffsetSpriteChildWindow : EditorWindow
{
    private const string PreviewChildName = "Visual (preview)";

    private GameObject _target;
    private SpriteRenderer _targetSr;
    private GameObject _previewChild;
    private Vector2 _offset;
    private bool _moveCollider = true;
    private readonly Dictionary<Collider2D, Vector2> _originalColliderOffsets = new();

    [MenuItem("Tools/Create Sprite Offset Child...")]
    private static void Open()
    {
        var window = GetWindow<CreateOffsetSpriteChildWindow>(true, "Create Offset Child");
        window.minSize = new Vector2(300, 175);
        window.maxSize = new Vector2(300, 175);
    }

    private void OnSelectionChange() => Repaint();

    private void OnDisable() => EndPreview();

    private void OnGUI()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != _target)
        {
            EndPreview();
            _target = selected;
            _targetSr = _target != null ? _target.GetComponent<SpriteRenderer>() : null;
            _offset = Vector2.zero;
        }

        EditorGUILayout.LabelField("Target", _target != null ? _target.name : "(select a GameObject)");

        if (_target != null && _targetSr == null)
            EditorGUILayout.HelpBox("Selected GameObject has no SpriteRenderer.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(_targetSr == null))
        {
            EditorGUI.BeginChangeCheck();
            _offset = EditorGUILayout.Vector2Field("Offset (world units)", _offset);
            _moveCollider = EditorGUILayout.Toggle(
                new GUIContent("Move existing colliders too",
                    "Shifts any Box/Circle/Capsule/Polygon Collider2D on this object by the same offset."),
                _moveCollider);
            if (EditorGUI.EndChangeCheck())
                UpdatePreview();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply"))
                    Apply();
                using (new EditorGUI.DisabledScope(_previewChild == null))
                {
                    if (GUILayout.Button("Revert"))
                        EndPreview();
                }
            }
        }

        if (_previewChild != null)
            EditorGUILayout.HelpBox("Previewing — not yet saved. Press Apply to commit.", MessageType.Info);
    }

    /// <summary>Creates (once) or repositions a throwaway preview child so the Scene view
    /// reflects the offset live while the parent's own renderer is hidden.</summary>
    private void UpdatePreview()
    {
        if (_targetSr == null) return;

        if (_previewChild == null)
        {
            _previewChild = new GameObject(PreviewChildName) { hideFlags = HideFlags.DontSave };
            _previewChild.transform.SetParent(_target.transform, worldPositionStays: false);
            _previewChild.transform.localRotation = Quaternion.identity;
            _previewChild.transform.localScale = Vector3.one;

            var previewSr = _previewChild.AddComponent<SpriteRenderer>();
            EditorUtility.CopySerialized(_targetSr, previewSr);

            _targetSr.enabled = false;
        }

        _previewChild.transform.localPosition = new Vector3(_offset.x, _offset.y, 0f);

        if (_moveCollider)
            PreviewColliders();
        else
            RestoreColliders();

        SceneView.RepaintAll();
    }

    /// <summary>Live-shifts every movable Collider2D on the target by the current offset,
    /// caching each one's original offset the first time it's touched.</summary>
    private void PreviewColliders()
    {
        foreach (var col in _target.GetComponents<Collider2D>())
        {
            if (!TryGetOffset(col, out Vector2 current)) continue;

            if (!_originalColliderOffsets.TryGetValue(col, out Vector2 original))
            {
                original = current;
                _originalColliderOffsets[col] = original;
            }

            SetOffset(col, original + _offset);
        }
    }

    /// <summary>Puts every collider touched by the preview back to its original offset.</summary>
    private void RestoreColliders()
    {
        foreach (var kv in _originalColliderOffsets)
            if (kv.Key != null)
                SetOffset(kv.Key, kv.Value);
        _originalColliderOffsets.Clear();
    }

    /// <summary>Discards the preview child, if any, and restores the parent's own renderer
    /// and collider offsets.</summary>
    private void EndPreview()
    {
        if (_previewChild != null)
        {
            DestroyImmediate(_previewChild);
            _previewChild = null;
        }
        if (_targetSr != null)
            _targetSr.enabled = true;
        RestoreColliders();
        SceneView.RepaintAll();
    }

    private void Apply()
    {
        if (_targetSr == null) return;

        var colliderShifts = new Dictionary<Collider2D, Vector2>();
        if (_moveCollider)
        {
            foreach (var col in _target.GetComponents<Collider2D>())
            {
                if (!TryGetOffset(col, out Vector2 current)) continue;
                colliderShifts[col] = _originalColliderOffsets.TryGetValue(col, out Vector2 original)
                    ? original
                    : current;
            }
        }

        EndPreview();
        CreateChild(_target, _targetSr, _offset);
        _targetSr = null; // the parent's SpriteRenderer no longer exists

        foreach (var kv in colliderShifts)
        {
            if (kv.Key == null) continue;
            Undo.RecordObject(kv.Key, "Create Offset Sprite Child");
            SetOffset(kv.Key, kv.Value + _offset);
        }

        Close();
    }

    private static bool TryGetOffset(Collider2D collider, out Vector2 offset)
    {
        switch (collider)
        {
            case BoxCollider2D box: offset = box.offset; return true;
            case CircleCollider2D circle: offset = circle.offset; return true;
            case CapsuleCollider2D capsule: offset = capsule.offset; return true;
            case PolygonCollider2D polygon: offset = polygon.offset; return true;
            default: offset = default; return false;
        }
    }

    private static void SetOffset(Collider2D collider, Vector2 offset)
    {
        switch (collider)
        {
            case BoxCollider2D box: box.offset = offset; break;
            case CircleCollider2D circle: circle.offset = offset; break;
            case CapsuleCollider2D capsule: capsule.offset = offset; break;
            case PolygonCollider2D polygon: polygon.offset = offset; break;
        }
    }

    private static void CreateChild(GameObject parent, SpriteRenderer parentSr, Vector2 offset)
    {
        var child = new GameObject("Visual");
        Undo.RegisterCreatedObjectUndo(child, "Create Offset Sprite Child");
        Undo.SetTransformParent(child.transform, parent.transform, "Create Offset Sprite Child");
        child.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        var childSr = Undo.AddComponent<SpriteRenderer>(child);
        EditorUtility.CopySerialized(parentSr, childSr);

        Undo.DestroyObjectImmediate(parentSr);

        Selection.activeGameObject = child;
    }
}
