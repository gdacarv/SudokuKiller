using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class CluesTooltip : HoverTooltip
{
    [Header("Clue Lines")]
    public List<LocalizedString> lines = new();

    protected override void ShowTooltip()
    {
        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);

        var nameLabel = GetComponent<NameLabel>();
        string characterName = nameLabel != null ? nameLabel.GetLocalizedName() : gameObject.name;

        var resolved = new List<string>(lines.Count);
        foreach (var ls in lines)
            resolved.Add(ls != null ? ls.GetLocalizedString(characterName) : "");

        tooltipUI.SetLines(resolved);
        PositionTooltip();
    }
}
