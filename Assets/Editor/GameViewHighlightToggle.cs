using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class GameViewHighlightToggle
{
    static GameViewHighlightToggle()
    {
        EditorApplication.update += EnsureToggleInGameViews;
    }

    static void EnsureToggleInGameViews()
    {
        var gm = Object.FindFirstObjectByType<GridManager>();

        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
            foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType))
                EnsureToggleInWindow((EditorWindow)w, gm);

        foreach (SceneView sv in SceneView.sceneViews)
            EnsureToggleInWindow(sv, gm);
    }

    static void EnsureToggleInWindow(EditorWindow window, GridManager gm)
    {
        var root = window.rootVisualElement;
        if (root.Q<Toggle>("highlight-violations-toggle") != null) return;

        var toggle = new Toggle("Highlight Violations")
        {
            name = "highlight-violations-toggle",
            value = gm != null && gm.highlightRuleViolations
        };

        toggle.style.alignSelf = Align.FlexEnd;
        toggle.style.top = window is SceneView ? 5 : (Application.isPlaying ? 44 : 24);
        toggle.style.position = Position.Absolute;
        toggle.style.right = 5;
        toggle.style.color = Color.white;
        toggle.style.unityFontStyleAndWeight = FontStyle.Normal;

        toggle.RegisterValueChangedCallback(evt =>
        {
            var g = Object.FindFirstObjectByType<GridManager>();
            if (g == null) return;
            g.highlightRuleViolations = evt.newValue;
            g.RefreshViolationHighlights();
        });

        root.Add(toggle);
    }
}
