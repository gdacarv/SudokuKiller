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

        var resolved = new List<string>(lines.Count);
        foreach (var ls in lines)
            resolved.Add(ls != null ? ls.GetLocalizedString() : "");

        tooltipUI.SetLines(resolved);
        PositionTooltip();
    }
}
