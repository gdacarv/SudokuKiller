using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LevelSelectController : MonoBehaviour
{
    [SerializeField] private List<LevelDefinition> levels = new();
    [SerializeField] private Transform gridContent;
    [SerializeField] private LevelItemView itemPrefab;

    public event Action<string> LevelSelected;

    IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        PopulateGrid();
    }

    void PopulateGrid()
    {
        if (gridContent == null || itemPrefab == null) return;
        foreach (var def in levels)
        {
            if (def == null) continue;
            var item = Instantiate(itemPrefab, gridContent);
            item.Bind(def, OnLevelClicked);
        }
    }

    void OnLevelClicked(LevelDefinition def)
    {
        LevelSelected?.Invoke(def.sceneName);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (gridContent == null)
            gridContent = transform.Find("Grid");
    }
#endif
}
