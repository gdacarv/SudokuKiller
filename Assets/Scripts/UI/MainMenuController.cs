using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI playLabel;
    [SerializeField] private TextMeshProUGUI quitLabel;

    [Header("Localization")]
    [SerializeField] private LocalizedString playTextLocalized;
    [SerializeField] private LocalizedString quitTextLocalized;

    [Header("Timings")]
    [SerializeField] private float backgroundFadeDuration = 0.8f;
    [SerializeField] private float contentFadeDuration = 0.5f;
    [SerializeField] private float contentDelay = 0.2f;

    [Header("Scene Loading")]
    [SerializeField] private string gameSceneName = "IceCreamShopScene";

    void Awake()
    {
        if (blackOverlay != null) blackOverlay.alpha = 1f;
        if (contentGroup != null)
        {
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
        }
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        ApplyLabels();
        yield return FadeInRoutine();
        FocusFirstButton();
    }

    void OnLocaleChanged(Locale _) => ApplyLabels();

    void ApplyLabels()
    {
        if (playLabel != null && !playTextLocalized.IsEmpty)
            playLabel.text = playTextLocalized.GetLocalizedString();
        if (quitLabel != null && !quitTextLocalized.IsEmpty)
            quitLabel.text = quitTextLocalized.GetLocalizedString();
    }

    IEnumerator FadeInRoutine()
    {
        yield return FadeCanvasGroup(blackOverlay, 1f, 0f, backgroundFadeDuration);
        if (blackOverlay != null) blackOverlay.blocksRaycasts = false;

        if (contentDelay > 0f) yield return new WaitForSeconds(contentDelay);

        yield return FadeCanvasGroup(contentGroup, 0f, 1f, contentFadeDuration);
        if (contentGroup != null)
        {
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - n, 3f));
            yield return null;
        }
        cg.alpha = to;
    }

    void FocusFirstButton()
    {
        if (EventSystem.current == null || playButton == null) return;
        EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    void OnPlayClicked()
    {
        if (contentGroup != null) contentGroup.interactable = false;
        StartCoroutine(LoadGameRoutine());
    }

    IEnumerator LoadGameRoutine()
    {
        if (blackOverlay != null) blackOverlay.blocksRaycasts = true;
        float t = 0f;
        while (t < backgroundFadeDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / backgroundFadeDuration);
            if (blackOverlay != null) blackOverlay.alpha = n * n * n;
            yield return null;
        }
        if (blackOverlay != null) blackOverlay.alpha = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
