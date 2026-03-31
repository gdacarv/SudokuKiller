using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class GameViewHighlightToggle
{
    static GameViewHighlightToggle()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            AddToggleToGameViews();
        else if (state == PlayModeStateChange.ExitingPlayMode)
            RemoveToggleFromGameViews();
    }

    static void AddToggleToGameViews()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null) return;

        var gm = Object.FindFirstObjectByType<GridManager>();

        foreach (var gameView in Resources.FindObjectsOfTypeAll(gameViewType))
        {
            var root = ((EditorWindow)gameView).rootVisualElement;

            if (root.Q<Toggle>("highlight-violations-toggle") != null) continue;

            var toggle = new Toggle("Highlight Violations")
            {
                name = "highlight-violations-toggle",
                value = gm != null && gm.highlightRuleViolations
            };

            toggle.style.alignSelf = Align.FlexEnd;
            toggle.style.top = 44;
            toggle.style.position = Position.Absolute;
            toggle.style.right = 4;
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

    static void RemoveToggleFromGameViews()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null) return;

        foreach (var gameView in Resources.FindObjectsOfTypeAll(gameViewType))
        {
            var root = ((EditorWindow)gameView).rootVisualElement;
            root.Q<Toggle>("highlight-violations-toggle")?.RemoveFromHierarchy();
        }
    }
}
