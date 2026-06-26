using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Always-visible localized tips displayed in the "General Tips" panel at the bottom of the board.
/// Attach to the Board GameObject (alongside GridManager) and point <see cref="contentRoot"/> at the
/// "General Tips" panel Transform. Tips are split across <see cref="columns"/> word-wrapped columns.
/// </summary>
[ExecuteAlways]
public class GeneralTips : MonoBehaviour
{
    private const string ColumnPrefix = "__TipColumn_";
    private const string ColumnSuffix = "__";

    [Header("Tips")]
    [Tooltip(
        "CAUTION — List<LocalizedString> duplication bug: pressing '+' or Duplicate in the Inspector " +
        "copies SerializeReference rids, so all duplicated entries end up sharing the same Local Variable " +
        "instances. Tips with no Local Variables (the common case) are safe. If you do add Smart String " +
        "Local Variables to an entry, immediately clear and re-add them after pressing '+' so each entry " +
        "gets a unique rid. See memory: feedback_localizedstring_list_duplication.")]
    public List<LocalizedString> tips = new();

    [Min(1)]
    [Tooltip("Number of columns to distribute tips across.")]
    public int columns = 4;

    [Header("Layout")]
    [Tooltip("Parent Transform the column text objects are created under. " +
        "Defaults to the 'General Tips' GameObject in the scene if left blank.")]
    public Transform contentRoot;
    [Tooltip("Local position (within contentRoot) of the first column's top-left corner.")]
    public Vector2 startLocalPosition = new Vector2(0.5f, 0f);
    [Tooltip("Width of each column in world units. Controls word-wrap width.")]
    public float columnWidth = 3f;
    [Tooltip("Gap between columns in world units.")]
    public float columnSpacing = 0.2f;

    [Header("Style")]
    [Tooltip("Font asset to use. Should match the 'Other Clues' header font.")]
    public TMP_FontAsset fontAsset;
    public float fontSize = 2f;
    [Tooltip("Text color. Default matches the dark-red 'Other Clues' header.")]
    public Color textColor = new Color(0.5943396f, 0.006270661f, 0f, 1f);
    public string bulletPrefix = "• ";

    private bool _localeSubscribed;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        AutoWireContentRoot();
        Rebuild();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _localeSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_localeSubscribed)
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            _localeSubscribed = false;
        }
    }

    private void OnLocaleChanged(Locale _) => Rebuild();

    private void OnValidate()
    {
        AutoWireContentRoot();
#if UNITY_EDITOR
        // Defer to avoid "SendMessage cannot be called during Awake/OnEnable" restrictions
        // and the "cannot destroy during serialization" error when column count changes.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Rebuild();
        };
#endif
    }

    // ── Auto-wire ────────────────────────────────────────────────────────────

    private void AutoWireContentRoot()
    {
        if (contentRoot != null) return;
        var go = GameObject.Find("General Tips");
        if (go != null) contentRoot = go.transform;
    }

    // ── Rebuild ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves all tips and refreshes the column TextMeshPro objects under contentRoot.
    /// Called automatically on Awake, locale change, and OnValidate (edit-time preview).
    /// </summary>
    public void Rebuild()
    {
        if (contentRoot == null) return;
        if (columns < 1) columns = 1;

        // Resolve tips to strings
        var resolved = new List<string>(tips.Count);
        foreach (var ls in tips)
        {
            if (ls == null || ls.IsEmpty) continue;
            string text;
            if (Application.isPlaying)
            {
                text = ls.GetLocalizedString();
            }
            else
            {
                // In the editor, localization may not be initialized; fall back gracefully.
                try { text = ls.GetLocalizedString(); }
                catch { text = "[tip]"; }
            }
            resolved.Add(text);
        }

        // Distribute tips in contiguous chunks, column-major (left to right)
        int rowsPerCol = resolved.Count == 0 ? 0 : Mathf.CeilToInt((float)resolved.Count / columns);

        for (int col = 0; col < columns; col++)
        {
            var tmp = GetOrCreateColumn(col);
            ApplyColumnStyle(tmp, col);

            int startIdx = col * rowsPerCol;
            if (resolved.Count == 0 || startIdx >= resolved.Count)
            {
                tmp.text = "";
            }
            else
            {
                int endIdx = Mathf.Min(startIdx + rowsPerCol, resolved.Count);
                var sb = new System.Text.StringBuilder();
                for (int i = startIdx; i < endIdx; i++)
                {
                    if (i > startIdx) sb.Append('\n');
                    sb.Append(bulletPrefix).Append(resolved[i]);
                }
                tmp.text = sb.ToString();
            }

            tmp.ForceMeshUpdate();
        }

        // Remove any extra columns left over from a previous higher column count
        DestroyColumnsFrom(columns);
    }

    // ── Column object management ─────────────────────────────────────────────

    private TextMeshPro GetOrCreateColumn(int index)
    {
        string objName = $"{ColumnPrefix}{index}{ColumnSuffix}";
        Transform existing = contentRoot.Find(objName);
        if (existing != null)
            return existing.GetComponent<TextMeshPro>();

        var go = new GameObject(objName);
        // Hide from the hierarchy so the hand-crafted scene structure stays uncluttered
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
        go.transform.SetParent(contentRoot, false);
        return go.AddComponent<TextMeshPro>();
    }

    private void ApplyColumnStyle(TextMeshPro tmp, int columnIndex)
    {
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.autoSizeTextContainer = false;

        // Pivot (0,1) = top-left so localPosition is exactly where the text starts,
        // making startLocalPosition straightforward to set in the Inspector.
        tmp.rectTransform.pivot = new Vector2(0f, 1f);
        // Width drives word-wrap; height left large so overflow text isn't clipped.
        tmp.rectTransform.sizeDelta = new Vector2(columnWidth, 10f);

        float x = startLocalPosition.x + columnIndex * (columnWidth + columnSpacing);
        tmp.transform.localPosition = new Vector3(x, startLocalPosition.y, -0.1f);

        var rend = tmp.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sortingLayerName = "UI";
            rend.sortingOrder = 10;
        }
    }

    private void DestroyColumnsFrom(int startIndex)
    {
        for (int i = startIndex; ; i++)
        {
            string objName = $"{ColumnPrefix}{i}{ColumnSuffix}";
            Transform t = contentRoot.Find(objName);
            if (t == null) break;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }
    }
}
