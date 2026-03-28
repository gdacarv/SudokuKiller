using UnityEngine;

public class PopupMessage : MonoBehaviour
{
    public static PopupMessage Instance { get; private set; }

    [SerializeField] private TooltipUI popupUI;
    [SerializeField] private float defaultDuration = 2.5f;

    private void Awake()
    {
        Instance = this;
    }

    public static void Show(string message, float duration = -1f)
    {
        if (Instance == null || Instance.popupUI == null) return;
        float d = duration < 0f ? Instance.defaultDuration : duration;
        // Activate before ShowPopup so StartCoroutine works on the inactive panel
        Instance.popupUI.gameObject.SetActive(true);
        Instance.popupUI.ShowPopup(message, d);
    }
}
