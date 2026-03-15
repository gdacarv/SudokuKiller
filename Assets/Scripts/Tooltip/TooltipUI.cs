using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    public void SetLines(List<string> lines)
    {
        if (text == null) return;

        if (lines == null || lines.Count == 0)
        {
            text.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<b>Clues:</b>");
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append('\n').Append("\u2022 ").Append(lines[i]);
        }
        text.text = sb.ToString();
        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
