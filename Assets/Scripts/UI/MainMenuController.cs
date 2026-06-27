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

    [Header("Level Select")]
    [SerializeField] private CanvasGroup levelSelectGroup;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI backLabel;
    [SerializeField] private LevelSelectController levelSelectController;

    [Header("Localization")]
    [SerializeField] private LocalizedString playTextLocalized;
    [SerializeField] private LocalizedString quitTextLocalized;
    [SerializeField] private LocalizedString backTextLocalized;

    [Header("Timings")]
    [SerializeField] private float backgroundFadeDuration = 0.8f;
    [SerializeField] private float contentFadeDuration = 0.5f;
    [SerializeField] private float contentDelay = 0.2f;

    void Awake()
    {
        if (blackOverlay != null) blackOverlay.alpha = 1f;
        if (contentGroup != null)
        {
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
        }
        if (levelSelectGroup != null)
        {
            levelSelectGroup.alpha = 0f;
            levelSelectGroup.interactable = false;
            levelSelectGroup.blocksRaycasts = false;
        }
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (levelSelectController != null) levelSelectController.LevelSelected += OnLevelSelected;
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        if (levelSelectController != null) levelSelectController.LevelSelected -= OnLevelSelected;
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
        if (backLabel != null && !backTextLocalized.IsEmpty)
            backLabel.text = backTextLocalized.GetLocalizedString();
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
        StartCoroutine(CrossFadeToLevelSelect());
    }

    void OnBackClicked()
    {
        if (levelSelectGroup != null) levelSelectGroup.interactable = false;
        StartCoroutine(CrossFadeToMainMenu());
    }

    void OnLevelSelected(string sceneName)
    {
        if (levelSelectGroup != null) levelSelectGroup.interactable = false;
        StartCoroutine(LoadGameRoutine(sceneName));
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator CrossFadeToLevelSelect()
    {
        yield return FadeCanvasGroup(contentGroup, 1f, 0f, contentFadeDuration);
        if (contentGroup != null) contentGroup.blocksRaycasts = false;

        yield return FadeCanvasGroup(levelSelectGroup, 0f, 1f, contentFadeDuration);
        if (levelSelectGroup != null)
        {
            levelSelectGroup.interactable = true;
            levelSelectGroup.blocksRaycasts = true;
        }
    }

    IEnumerator CrossFadeToMainMenu()
    {
        yield return FadeCanvasGroup(levelSelectGroup, 1f, 0f, contentFadeDuration);
        if (levelSelectGroup != null) levelSelectGroup.blocksRaycasts = false;

        yield return FadeCanvasGroup(contentGroup, 0f, 1f, contentFadeDuration);
        if (contentGroup != null)
        {
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;
        }
    }

    IEnumerator LoadGameRoutine(string sceneName)
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
        SceneManager.LoadScene(sceneName);
    }
}
