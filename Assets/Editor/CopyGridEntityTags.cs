using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Copies the resolved tags of the selected GridEntity to the clipboard in the same GenericPropertyJSON
/// format Unity uses when copying a List<TagEntry> property, so it can be pasted straight into any tag
/// list (right-click > Paste Property) such as GridEntity.tags or the Hover Flash Tags sets.
public static class CopyGridEntityTags
{
    const string MenuPath = "Tools/Sudoku Killers/Copy GridEntity Tags %#&c";

    [MenuItem(MenuPath)]
    static void CopySelected()
    {
        var entity = FindSelectedEntity();
        if (entity == null) return;
        Copy(entity);
    }

    [MenuItem(MenuPath, true)]
    static bool CopySelectedValidate() => FindSelectedEntity() != null;

    // Same action from the component's right-click / gear menu.
    [MenuItem("CONTEXT/GridEntity/Copy Tags")]
    static void CopyFromContext(MenuCommand command) => Copy((GridEntity)command.context);

    // Only one tag list can be pasted at a time: prefer the active object, else the first selected entity.
    static GridEntity FindSelectedEntity()
    {
        var active = Selection.activeGameObject;
        var entity = active != null ? active.GetComponent<GridEntity>() : null;
        return entity != null
            ? entity
            : Selection.gameObjects.Select(go => go.GetComponent<GridEntity>()).FirstOrDefault(e => e != null);
    }

    static void Copy(GridEntity entity)
    {
        EditorGUIUtility.systemCopyBuffer = ToPropertyJson(entity);
        Debug.Log($"[CopyGridEntityTags] Copied {entity.ResolvedTags.Count} tag(s) from \"{entity.name}\".");
    }

    // Mirrors Unity's clipboard JSON for an array property of structs, character for character.
    public static string ToPropertyJson(GridEntity entity)
    {
        var tags = entity.ResolvedTags;
        int n = tags.Count;

        var sb = new StringBuilder();
        sb.Append("GenericPropertyJSON:{\"name\":\"tags\",\"type\":-1,\"arraySize\":").Append(n)
          .Append(",\"arrayType\":\"TagEntry\",\"children\":[{\"name\":\"Array\",\"type\":-1,\"arraySize\":").Append(n)
          .Append(",\"arrayType\":\"TagEntry\",\"children\":[{\"name\":\"size\",\"type\":12,\"val\":").Append(n).Append('}');

        foreach (var t in tags)
        {
            sb.Append(",{\"name\":\"data\",\"type\":-1,\"children\":[{\"name\":\"key\",\"type\":3,\"val\":");
            AppendJsonString(sb, t.key);
            sb.Append("},{\"name\":\"value\",\"type\":3,\"val\":");
            AppendJsonString(sb, t.value);
            sb.Append("}]}");
        }

        sb.Append("]}]}");
        return sb.ToString();
    }

    static void AppendJsonString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s ?? string.Empty)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
