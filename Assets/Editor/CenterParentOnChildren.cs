using UnityEditor;
using UnityEngine;

public static class CenterParentOnChildren
{
    [MenuItem("CONTEXT/Transform/Center On Children")]
    static void CenterOnChildren(MenuCommand cmd)
    {
        var parent = (Transform)cmd.context;
        if (parent.childCount == 0) return;

        Vector3 center = Vector3.zero;
        foreach (Transform child in parent)
            center += child.position;
        center /= parent.childCount;

        var worldPositions = new Vector3[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
            worldPositions[i] = parent.GetChild(i).position;

        Undo.RecordObject(parent, "Center Parent On Children");
        parent.position = center;

        for (int i = 0; i < parent.childCount; i++)
        {
            Undo.RecordObject(parent.GetChild(i), "Center Parent On Children");
            parent.GetChild(i).position = worldPositions[i];
        }
    }
}
