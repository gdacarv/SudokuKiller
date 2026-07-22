using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class CluesTooltip : HoverTooltip
{
    [Header("Clue Lines")]
    public List<LocalizedString> lines = new();

    protected override void ShowTooltip()
    {
        var nameLabel = GetComponent<NameLabel>();
        string characterName = nameLabel != null ? nameLabel.GetLocalizedName() : gameObject.name;

        var resolved = new List<string>(lines.Count);
        foreach (var ls in lines)
        {
            string text = ls != null ? ls.GetLocalizedString(characterName) : "";
            if (!string.IsNullOrWhiteSpace(text))
                resolved.Add(text);
        }

        if (resolved.Count == 0)
        {
            _isShowing = false;
            return;
        }

        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetLines(resolved);
        PositionTooltip();
    }
}
