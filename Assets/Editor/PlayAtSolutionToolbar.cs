using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

/// <summary>
/// Adds a "Play (Solution)" button to the main toolbar that enters play mode with
/// GridManager.startAtSolutionPositions forced to true. The flag is consumed by
/// GridManager.Awake, so the scene is never modified and a normal Play is unaffected.
/// </summary>
public static class PlayAtSolutionToolbar
{
    public const string SessionKey = "PlayAtSolutionPositions";

    [MainToolbarElement("SudoKillers/PlayAtSolution", defaultDockPosition = MainToolbarDockPosition.Middle)]
    public static MainToolbarElement CreateButton()
    {
        // Built-in play icon (dark/light skin aware) so it reads like the native Play button
        var icon = EditorGUIUtility.IconContent("d_PlayButton").image as Texture2D;
        var content = new MainToolbarContent("Solution", icon, "Play with Start At Solution Positions enabled");
        return new MainToolbarButton(content, PlayAtSolution);
    }

    [MenuItem("Tools/Sudoku Killers/Play At Solution Positions %#&p")]
    public static void PlayAtSolution()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }
}
