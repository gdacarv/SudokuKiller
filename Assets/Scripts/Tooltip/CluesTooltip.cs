using System.Collections.Generic;
using UnityEngine;

public class CluesTooltip : HoverTooltip
{
    [Header("Clue Lines")]
    public List<string> lines = new();

    protected override void ShowTooltip()
    {
        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetLines(lines);
        PositionTooltip();
    }
}
