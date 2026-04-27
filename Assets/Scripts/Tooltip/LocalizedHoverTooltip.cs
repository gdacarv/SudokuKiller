using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LocalizedHoverTooltip : HoverTooltip
{
    [Header("Localized Content")]
    [SerializeField] private LocalizedString messageLocalized;

    public LocalizedString LocalizedMessage => messageLocalized;

    void OnEnable()
    {
        if (Application.isPlaying)
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Application.isPlaying)
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        if (_isShowing && tooltipUI != null)
            tooltipUI.SetText(Resolve());
    }

    protected override void ShowTooltip()
    {
        _isShowing = true;
        tooltipUI.gameObject.SetActive(true);
        tooltipUI.SetText(Resolve());
        PositionTooltip();
    }

    private string Resolve()
    {
        if (messageLocalized != null && !messageLocalized.IsEmpty)
            return messageLocalized.GetLocalizedString();
        return message;
    }
}
