using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Swaps a Button's sprite set (idle/selected/pressed) whenever the active locale changes.
/// Attach to a Button GameObject alongside the Image that is the button's TargetGraphic.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class LocalizedButtonSprites : MonoBehaviour, IPointerEnterHandler
{
    [System.Serializable]
    public struct StateSprites
    {
        public Sprite idle;
        public Sprite selected;
        public Sprite pressed;
    }

    [Header("Components")]
    [SerializeField] private Button button;
    [SerializeField] private Image targetImage;

    [Header("Sprites by Language")]
    [SerializeField] private StateSprites english;
    [SerializeField] private StateSprites portuguese;

    [Header("Locale Detection")]
    [Tooltip("Locale code prefix for English. Any other locale falls back to Portuguese.")]
    [SerializeField] private string englishLocaleCode = "en";

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Reset()
    {
        AutoWire();
    }

    void OnValidate()
    {
        AutoWire();
    }

    void AutoWire()
    {
        if (button == null) button = GetComponent<Button>();
        if (targetImage == null) targetImage = GetComponent<Image>();
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        // Apply immediately if localization is already initialized
        if (LocalizationSettings.SelectedLocale != null)
            Apply(LocalizationSettings.SelectedLocale);
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    IEnumerator Start()
    {
        // Wait for the localization system to finish initializing before the first apply.
        // This prevents a wrong-locale flash on startup (mirrors MainMenuController pattern).
        yield return LocalizationSettings.InitializationOperation;
        Apply(LocalizationSettings.SelectedLocale);
    }

    // Transfer EventSystem selection to this button on mouse enter so that at most
    // one button shows as highlighted at a time (avoids dual-highlight when one button
    // is keyboard-selected and the mouse hovers over a different one).
    public void OnPointerEnter(PointerEventData _)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    void OnLocaleChanged(Locale locale) => Apply(locale);

    void Apply(Locale locale)
    {
        if (locale == null) return;

        bool isEnglish = locale.Identifier.Code.StartsWith(englishLocaleCode,
            System.StringComparison.OrdinalIgnoreCase);

        StateSprites set = isEnglish ? english : portuguese;

        if (set.idle == null) return; // sprites not yet assigned in editor

        // Drive the target image sprite (idle state / normal appearance)
        if (targetImage != null)
        {
            targetImage.sprite = set.idle;
            targetImage.color = Color.white;
            targetImage.preserveAspect = true;
            targetImage.SetNativeSize();
        }

        // Switch to SpriteSwap and fill all state slots
        if (button != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = set.selected,
                pressedSprite     = set.pressed,
                selectedSprite    = set.selected,
                disabledSprite    = set.idle,
            };
        }
    }
}
