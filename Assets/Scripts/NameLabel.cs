using TMPro;
using UnityEngine;

[ExecuteAlways]
public class NameLabel : MonoBehaviour
{
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private float _fontSize = 2f;
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private TextAlignmentOptions _alignment = TextAlignmentOptions.Center;
    [SerializeField] private FontStyles _fontStyle = FontStyles.Normal;
    [SerializeField] private int _sortingOrder = 10;

    private GameObject _labelObject;
    private TextMeshPro _textMesh;

    private void Awake()
    {
        CreateLabel();
    }

    private void OnValidate()
    {
        if (_textMesh != null)
            ApplySettings();
    }

    private void CreateLabel()
    {
        // Reuse existing label child if present (e.g. after domain reload)
        Transform existing = transform.Find("__NameLabel__");
        if (existing != null)
        {
            _labelObject = existing.gameObject;
            _textMesh = _labelObject.GetComponent<TextMeshPro>();
        }
        else
        {
            _labelObject = new GameObject("__NameLabel__");
            _labelObject.transform.SetParent(transform, false);
            _textMesh = _labelObject.AddComponent<TextMeshPro>();
        }

        ApplySettings();
    }

    private void ApplySettings()
    {
        _labelObject.transform.localPosition = _offset;
        _textMesh.text = gameObject.name;
        _textMesh.fontSize = _fontSize;
        _textMesh.color = _color;
        _textMesh.alignment = _alignment;
        _textMesh.autoSizeTextContainer = true;

        _textMesh.fontStyle = _fontStyle;
        _textMesh.sortingOrder = _sortingOrder;
    }
}
