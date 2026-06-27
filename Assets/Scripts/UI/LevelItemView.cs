using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class LevelItemView : MonoBehaviour
{
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private Button button;

    private LevelDefinition _def;
    private Action<LevelDefinition> _onClick;

    public void Bind(LevelDefinition def, Action<LevelDefinition> onClick)
    {
        _def = def;
        _onClick = onClick;
        if (thumbnailImage != null)
            thumbnailImage.sprite = def.thumbnail;
        ApplyLabel();
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (button != null) button.onClick.AddListener(OnButtonClicked);
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (button != null) button.onClick.RemoveListener(OnButtonClicked);
    }

    void OnLocaleChanged(Locale _) => ApplyLabel();

    void ApplyLabel()
    {
        if (nameLabel == null || _def == null) return;
        if (!_def.levelName.IsEmpty)
            nameLabel.text = _def.levelName.GetLocalizedString();
    }

    void OnButtonClicked() => _onClick?.Invoke(_def);
}
