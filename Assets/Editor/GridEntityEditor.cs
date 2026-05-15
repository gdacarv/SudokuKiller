using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridEntity))]
[CanEditMultipleObjects]
public class GridEntityEditor : Editor
{
    SerializedProperty _inheritProp;
    SerializedProperty _tagsProp;

    void OnEnable()
    {
        _inheritProp = serializedObject.FindProperty("inheritTagsFromParent");
        _tagsProp = serializedObject.FindProperty("tags");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        bool newInherit = EditorGUILayout.ToggleLeft(
            new GUIContent("Inherit Tags From Parent", _inheritProp.tooltip),
            _inheritProp.boolValue);
        if (EditorGUI.EndChangeCheck())
            _inheritProp.boolValue = newInherit;

        if (_inheritProp.boolValue && !_inheritProp.hasMultipleDifferentValues)
        {
            var entity = (GridEntity)target;
            var parent = entity.ParentEntity;

            if (parent == null)
            {
                EditorGUILayout.HelpBox(
                    "Inherit Tags From Parent is enabled, but no ancestor GridEntity was found. No tags will apply.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"Tags inherited from \"{parent.name}\".", MessageType.Info);

                using (new EditorGUI.DisabledScope(true))
                {
                    var resolved = entity.ResolvedTags;
                    if (resolved.Count == 0)
                    {
                        EditorGUILayout.LabelField("(no tags)");
                    }
                    else
                    {
                        foreach (var t in resolved)
                            EditorGUILayout.LabelField(t.key, t.value);
                    }
                }
            }
        }
        else
        {
            EditorGUILayout.PropertyField(_tagsProp, true);
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "inheritTagsFromParent", "tags");

        if (serializedObject.ApplyModifiedProperties())
        {
            var manager = FindObjectOfType<GridManager>();
            if (manager != null)
                manager.RefreshEditModeViolations();
        }
    }
}
