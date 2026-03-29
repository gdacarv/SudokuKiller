using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    [Header("Localization")]
    [SerializeField] private LocalizedString cluesHeaderLocalized;

    [Header("Popup Animation")]
    [SerializeField] private float slideAnimDuration = 0.3f;
    [SerializeField] private float slideOffsetY = 120f;

    private RectTransform _rect;
    private RectTransform Rect => _rect ??= GetComponent<RectTransform>();
    private Coroutine _popupCoroutine;
    private Vector2 _restAnchoredPos;

    public void SetText(string content)
    {
        if (text == null) return;
        text.text = content ?? "";
        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
    }

    public void SetLines(List<string> lines)
    {
        if (text == null) return;

        if (lines == null || lines.Count == 0)
        {
            text.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(cluesHeaderLocalized != null && !cluesHeaderLocalized.IsEmpty
            ? cluesHeaderLocalized.GetLocalizedString()
            : "<b>Clues:</b>");
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append('\n').Append("\u2022 ").Append(lines[i]);
        }
        text.text = sb.ToString();
        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
    }

    public void ShowPopup(string message, float holdDuration = 2.5f)
    {
        if (_popupCoroutine != null)
            StopCoroutine(_popupCoroutine);
        _popupCoroutine = StartCoroutine(PopupRoutine(message, holdDuration));
    }

    private IEnumerator PopupRoutine(string message, float holdDuration)
    {
        // Object is activated by caller before ShowPopup; set text and rebuild layout
        SetText(message);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);

        _restAnchoredPos = Rect.anchoredPosition;
        Vector2 hiddenPos = _restAnchoredPos + new Vector2(0f, slideOffsetY);

        // Slide in (from above)
        float t = 0f;
        while (t < slideAnimDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / slideAnimDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f); // ease-out cubic
            Rect.anchoredPosition = Vector2.Lerp(hiddenPos, _restAnchoredPos, eased);
            yield return null;
        }
        Rect.anchoredPosition = _restAnchoredPos;

        yield return new WaitForSeconds(holdDuration);

        // Slide out (upward)
        t = 0f;
        while (t < slideAnimDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / slideAnimDuration);
            float eased = normalized * normalized * normalized; // ease-in cubic
            Rect.anchoredPosition = Vector2.Lerp(_restAnchoredPos, hiddenPos, eased);
            yield return null;
        }

        gameObject.SetActive(false);
        Rect.anchoredPosition = _restAnchoredPos;
        _popupCoroutine = null;
    }
}
