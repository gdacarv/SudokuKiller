using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TilemapEditorExtensions
{
    [MenuItem("CONTEXT/Tilemap/Clear All Tiles")]
    static void ClearAllTiles(MenuCommand command)
    {
        Tilemap tilemap = (Tilemap)command.context;
        Undo.RecordObject(tilemap, "Clear All Tiles");
        tilemap.ClearAllTiles();
    }
}
