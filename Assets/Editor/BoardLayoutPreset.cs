using System.Collections.Generic;
using UnityEngine;

// One draggable's spot in a saved layout. Cells are two named ints on purpose:
// GridManager.WorldToCell returns (x = col, y = row) while the verifier uses
// (x = row, y = col), so a bare Vector2Int here would invite a swap bug.
[System.Serializable]
public struct BoardLayoutEntry
{
    public string  objectName;     // GameObject name — the project's identity key for draggables
    public int     row;            // -1 when the draggable is parked off the board
    public int     col;            // -1 when the draggable is parked off the board
    public Vector3 worldPosition;  // authoritative only when off the board

    public bool IsOnGrid => row >= 0 && col >= 0;
}

// A saved arrangement of every draggable in a scene. The layout's name is the
// asset's file name, so renaming it in the Project view and in the Board Layouts
// window are the same operation.
[CreateAssetMenu(menuName = "SudoKillers/Board Layout", fileName = "Layout")]
public class BoardLayoutPreset : ScriptableObject
{
    public string sceneName;
    public string capturedUtc;
    [TextArea] public string note;
    public List<BoardLayoutEntry> placements = new();
}
