#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CopySpriteSlicesTool
{
    [MenuItem("Tools/Copy Sprite Slices", validate = true)]
    static bool Validate()
    {
        if (Selection.objects.Length < 2) return false;
        foreach (var obj in Selection.objects)
        {
            if (!(obj is Texture2D)) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return false;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter)) return false;
        }
        return true;
    }

    [MenuItem("Tools/Copy Sprite Slices")]
    static void Execute()
    {
        var objects = Selection.objects;
        var paths = objects.Select(o => AssetDatabase.GetAssetPath(o)).ToArray();
        var importers = paths.Select(p => AssetImporter.GetAtPath(p) as TextureImporter).ToArray();
        var prefixes = paths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();

        SourcePickerWindow.Show(paths, importers, prefixes);
    }

    internal static void ApplySlices(int sourceIndex, string[] paths, TextureImporter[] importers, string[] prefixes)
    {
        var sourceImporter = importers[sourceIndex];
        var sourcePrefix = prefixes[sourceIndex];

        if (sourceImporter.spriteImportMode != SpriteImportMode.Multiple ||
            sourceImporter.spritesheet.Length == 0)
        {
            EditorUtility.DisplayDialog("Copy Sprite Slices",
                $"'{sourcePrefix}' has no slices to copy (must be Multiple sprite mode).", "OK");
            return;
        }

        var targetIndices = Enumerable.Range(0, paths.Length).Where(i => i != sourceIndex).ToArray();
        var targetsWithSlices = targetIndices.Where(i => importers[i].spritesheet.Length > 0).ToArray();

        if (targetsWithSlices.Length > 0)
        {
            bool ok = EditorUtility.DisplayDialog("Copy Sprite Slices",
                $"{targetsWithSlices.Length} of {targetIndices.Length} targets already have slices. Replace all?",
                "Replace", "Cancel");
            if (!ok) return;
        }

        var sourceSlices = sourceImporter.spritesheet;
        foreach (int ti in targetIndices)
        {
            var targetImporter = importers[ti];
            var targetPrefix = prefixes[ti];
            var targetPath = paths[ti];

            var newSlices = new SpriteMetaData[sourceSlices.Length];
            for (int i = 0; i < sourceSlices.Length; i++)
            {
                newSlices[i] = sourceSlices[i];
                var name = sourceSlices[i].name;
                newSlices[i].name = name.StartsWith(sourcePrefix)
                    ? targetPrefix + name.Substring(sourcePrefix.Length)
                    : name;
            }

            targetImporter.spriteImportMode = SpriteImportMode.Multiple;
            targetImporter.spritesheet = newSlices;
            EditorUtility.SetDirty(targetImporter);
            AssetDatabase.WriteImportSettingsIfDirty(targetPath);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        }

        var targetNames = string.Join(", ", targetIndices.Select(i => prefixes[i]));
        Debug.Log($"[CopySpriteSlices] Copied {sourceSlices.Length} slices from '{sourcePrefix}' to {targetIndices.Length} targets: {targetNames}.");
    }
}

class SourcePickerWindow : EditorWindow
{
    string[] _prefixes;
    string[] _paths;
    TextureImporter[] _importers;

    public static void Show(string[] paths, TextureImporter[] importers, string[] prefixes)
    {
        var win = CreateInstance<SourcePickerWindow>();
        win.titleContent = new GUIContent("Copy Sprite Slices");
        win._paths = paths;
        win._importers = importers;
        win._prefixes = prefixes;
        win.minSize = new Vector2(260, 40 + prefixes.Length * 26);
        win.maxSize = win.minSize;
        win.ShowUtility();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Copy slices FROM:", EditorStyles.boldLabel);
        GUILayout.Space(4);
        for (int i = 0; i < _prefixes.Length; i++)
        {
            int idx = i;
            if (GUILayout.Button(_prefixes[i]))
            {
                Close();
                CopySpriteSlicesTool.ApplySlices(idx, _paths, _importers, _prefixes);
            }
        }
        GUILayout.Space(4);
        if (GUILayout.Button("Cancel")) Close();
    }
}
#endif
