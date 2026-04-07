using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class RoomLabel : MonoBehaviour
{
    [SerializeField] private LocalizedString localizedRoomName;

    [SerializeField] private float _fontSize = 2f;
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private Vector2 _padding = new Vector2(0.15f, 0.1f);
    [SerializeField] private int _bgSortingOrder = 10;
    [SerializeField, Range(0f, 1f)] private float _bgAlpha = 0.85f;
    [SerializeField] private int _textSortingOrder = 11;

    [Header("Section")]
    [SerializeField] private bool _useSectionColor = false;
    [SerializeField] private int _sectionId = 0;

    private SpriteRenderer _bgRenderer;
    private GameObject _textObject;
    private TextMeshPro _textMesh;

    private void Awake()
    {
        _bgRenderer = GetComponent<SpriteRenderer>();
        CreateTextChild();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        ApplySettings();
    }

    private void OnValidate()
    {
        if (_bgRenderer == null)
            _bgRenderer = GetComponent<SpriteRenderer>();
        if (_textMesh != null)
            ApplySettings();
    }

    private void CreateTextChild()
    {
        Transform existing = transform.Find("__RoomLabelText__");
        if (existing != null)
        {
            _textObject = existing.gameObject;
            _textMesh = _textObject.GetComponent<TextMeshPro>();
        }
        else
        {
            _textObject = new GameObject("__RoomLabelText__");
            _textObject.transform.SetParent(transform, false);
            _textMesh = _textObject.AddComponent<TextMeshPro>();
        }

        _textObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;

        ApplySettings();
    }

    private string ResolveText()
    {
        return (Application.isPlaying && localizedRoomName != null && !localizedRoomName.IsEmpty)
            ? localizedRoomName.GetLocalizedString()
            : gameObject.name;
    }

public void ApplySettings()
    {
        if (_textMesh == null || _bgRenderer == null) return;

        _textMesh.text = ResolveText();
        _textMesh.fontSize = _fontSize;
        _textMesh.color = _textColor;
        _textMesh.alignment = TextAlignmentOptions.Center;
        _textMesh.enableWordWrapping = false;
        _textMesh.overflowMode = TextOverflowModes.Overflow;
        _textMesh.autoSizeTextContainer = true;
        _textMesh.sortingOrder = _textSortingOrder;
        _textMesh.sortingLayerID = _bgRenderer.sortingLayerID;

        _textMesh.ForceMeshUpdate();

        Vector2 textSize = _textMesh.GetRenderedValues(false);
        _bgRenderer.drawMode = SpriteDrawMode.Sliced;
        _bgRenderer.size = new Vector2(
            Mathf.Max(textSize.x + _padding.x * 2f, 0.25f),
            Mathf.Max(textSize.y + _padding.y * 2f, 0.35f)
        );
        _bgRenderer.sortingOrder = _bgSortingOrder;
        _bgRenderer.color = _useSectionColor ? SectionMarker.GetSectionColor(_sectionId, _bgAlpha) : Color.white.WithAlpha(_bgAlpha);

        _textObject.transform.localPosition = Vector3.zero;
    }
}

public static class ColorExtensions
{
    public static Color WithAlpha(this Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }
}