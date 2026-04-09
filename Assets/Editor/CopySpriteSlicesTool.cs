#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CopySpriteSlicesTool
{
    [MenuItem("Tools/Copy Sprite Slices", validate = true)]
    static bool Validate()
    {
        if (Selection.objects.Length != 2) return false;
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
        var pathA = AssetDatabase.GetAssetPath(objects[0]);
        var pathB = AssetDatabase.GetAssetPath(objects[1]);
        var importerA = AssetImporter.GetAtPath(pathA) as TextureImporter;
        var importerB = AssetImporter.GetAtPath(pathB) as TextureImporter;
        var prefixA = Path.GetFileNameWithoutExtension(pathA);
        var prefixB = Path.GetFileNameWithoutExtension(pathB);

        int choice = EditorUtility.DisplayDialogComplex(
            "Copy Sprite Slices",
            "Choose the copy direction:",
            $"{prefixA}  \u2192  {prefixB}",
            "Cancel",
            $"{prefixB}  \u2192  {prefixA}");

        if (choice == 1) return; // Cancel

        TextureImporter sourceImporter, targetImporter;
        string sourcePrefix, targetPrefix, targetPath;

        if (choice == 0)
        {
            sourceImporter = importerA; sourcePrefix = prefixA;
            targetImporter = importerB; targetPrefix = prefixB; targetPath = pathB;
        }
        else
        {
            sourceImporter = importerB; sourcePrefix = prefixB;
            targetImporter = importerA; targetPrefix = prefixA; targetPath = pathA;
        }

        if (sourceImporter.spriteImportMode != SpriteImportMode.Multiple ||
            sourceImporter.spritesheet.Length == 0)
        {
            EditorUtility.DisplayDialog("Copy Sprite Slices",
                $"'{sourcePrefix}' has no slices to copy (must be Multiple sprite mode).", "OK");
            return;
        }

        if (targetImporter.spritesheet.Length > 0)
        {
            bool ok = EditorUtility.DisplayDialog("Copy Sprite Slices",
                $"'{targetPrefix}' already has {targetImporter.spritesheet.Length} slices. Replace them?",
                "Replace", "Cancel");
            if (!ok) return;
        }

        var sourceSlices = sourceImporter.spritesheet;
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

        Debug.Log($"[CopySpriteSlices] Copied {newSlices.Length} slices from '{sourcePrefix}' to '{targetPrefix}'.");
    }
}
#endif
