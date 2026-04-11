using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

[InitializeOnLoad]
public static class SceneViewClueTooltip
{
    private const string PrefKey = "SceneViewClueTooltips_Enabled";

    static SceneViewClueTooltip()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    [MenuItem("Tools/Scene View Clue Tooltips")]
    private static void Toggle()
    {
        bool current = EditorPrefs.GetBool(PrefKey, false);
        EditorPrefs.SetBool(PrefKey, !current);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Scene View Clue Tooltips", true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked("Tools/Scene View Clue Tooltips", EditorPrefs.GetBool(PrefKey, false));
        return true;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!EditorPrefs.GetBool(PrefKey, false)) return;

        Event e = Event.current;

        if (e.type == EventType.MouseMove)
        {
            sceneView.Repaint();
            return;
        }

        if (e.type != EventType.Repaint) return;

        Vector2 mousePos = e.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        Vector2 worldPos = new Vector2(ray.origin.x, ray.origin.y);

        var tooltips = Object.FindObjectsByType<HoverTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var tooltip in tooltips)
        {
            if (!tooltip.gameObject.activeInHierarchy) continue;

            var col = tooltip.GetComponent<Collider2D>();
            if (col == null || !col.enabled) continue;

            if (!col.OverlapPoint(worldPos)) continue;

            List<string> lines = BuildLines(tooltip);
            if (lines.Count == 0) continue;

            DrawTooltip(sceneView, tooltip.transform.position, lines);
            break;
        }
    }

private static List<string> BuildLines(HoverTooltip tooltip)
    {
        var lines = new List<string>();

        if (tooltip is CluesTooltip clues)
        {
            string charName = tooltip.gameObject.name;
            lines.Add($"<b>{charName}</b>");

            foreach (var ls in clues.lines)
            {
                if (ls == null || ls.IsEmpty) continue;
                string text = ResolveLocalizedString(ls, charName);
                if (!string.IsNullOrEmpty(text))
                    lines.Add($"\u2022 {text}");
            }
        }
        else if (!string.IsNullOrEmpty(tooltip.message))
        {
            lines.Add(tooltip.message);
        }

        return lines;
    }

    private static void DrawTooltip(SceneView sceneView, Vector3 worldPos, List<string> lines)
    {
        Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
        string content = string.Join("\n", lines);

        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            richText = true,
            fontSize = 11,
            padding = new RectOffset(8, 8, 5, 5),
            wordWrap = false
        };
        style.normal.textColor = Color.white;

        Vector2 size = style.CalcSize(new GUIContent(content));
        size.x = Mathf.Min(size.x + 4f, 320f);
        size.y += 4f;

        float x = guiPos.x - size.x * 0.5f;
        float y = guiPos.y - size.y - 24f;

        x = Mathf.Clamp(x, 4f, sceneView.position.width - size.x - 4f);
        y = Mathf.Clamp(y, 4f, sceneView.position.height - size.y - 4f);

        Handles.BeginGUI();
        EditorGUI.DrawRect(new Rect(x - 1, y - 1, size.x + 2, size.y + 2), new Color(0f, 0f, 0f, 0.85f));
        GUI.Label(new Rect(x, y, size.x, size.y), content, style);
        Handles.EndGUI();
    }


private static string ResolveLocalizedString(LocalizedString ls, string charName)
    {
        // Try runtime path first (works when locale is initialized)
        if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
        {
            string result = ls.GetLocalizedString(charName);
            if (!string.IsNullOrEmpty(result))
                return Regex.Replace(result, @"\{[^}]+\}", charName);
        }

        // Editor fallback: read directly from the string table by entry ID
        try
        {
            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
            {
                if (collection.SharedData.TableCollectionNameGuid != ls.TableReference.TableCollectionNameGuid)
                    continue;

                StringTable table = null;
                foreach (var t in collection.StringTables)
                {
                    var st = t as StringTable;
                    if (st == null) continue;
                    if (table == null) table = st;
                    if (st.LocaleIdentifier.Code == "en") { table = st; break; }
                }

                if (table == null) continue;

                var entry = table.GetEntry(ls.TableEntryReference.KeyId);
                if (entry == null) continue;

                // Replace SmartFormat tokens like {char.adam} with the character name
                return Regex.Replace(entry.Value, @"\{[^}]+\}", charName);
            }
        }
        catch { }

        return null;
    }
}
