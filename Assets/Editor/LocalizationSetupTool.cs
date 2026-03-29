#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

public static class LocalizationSetupTool
{
    const string LocalesPath  = "Assets/Localization/Locales";
    const string TablesPath   = "Assets/Localization/Tables";
    const string SettingsPath = "Assets/Localization/LocalizationSettings.asset";

    public static void Setup()
    {
        EnsureDirectory(LocalesPath);
        EnsureDirectory(TablesPath);
        AssetDatabase.Refresh();

        SetupLocalizationSettings();
        var enLocale = CreateLocale("en");

        var ui = CreateStringTableCollection("UI", enLocale, new Dictionary<string, string>
        {
            ["tooltip.inactive"]        = "You need to place all the suspects, objects and victim\non the map before being able to identify the killer",
            ["tooltip.active"]          = "Click to identify the killer",
            ["popup.allSuspectsPlaced"] = "All suspects must be placed on the board!",
            ["popup.cluesNotRespected"] = "Some clues are not being respected!",
            ["cursor.identifyPrompt"]   = "Who is the killer?\nChoose wisely. There's no going back.",
            ["label.suspects"]          = "Suspects",
            ["tooltip.cluesHeader"]     = "<b>Clues:</b>",
        });

        var puzzle = CreateStringTableCollection("Puzzle", enLocale, new Dictionary<string, string>
        {
            ["clue.carla.0"]         = "Carla was the only person in his row and column",
            ["clue.carla.tooltip.0"] = "There was no other person in the same row and column of Carla",
            ["clue.bruno.0"]         = "Bruno was the only person in his row and column",
            ["clue.bruno.1"]         = "Bruno was alone in the room",
            ["clue.bruno.tooltip.0"] = "There was no other person in the same row and column of Bruno",
            ["clue.bruno.tooltip.1"] = "Bruno was the only person in the room",
            ["clue.adam.0"]          = "Adam was the only person in his row and column",
            ["clue.adam.tooltip.0"]  = "There was no other person in the same row and column of Adam",
            ["name.victim"]          = "Victim",
        });

        AssetDatabase.SaveAssets();
        WireSceneObjects(ui, puzzle);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] Done! Check Console for any warnings.");
    }

    // ─── Asset creation ───────────────────────────────────────────────────────

    static void EnsureDirectory(string assetPath)
    {
        var full = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }

    static void SetupLocalizationSettings()
    {
        if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
        var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
    }

    static Locale CreateLocale(string code)
    {
        string path = $"{LocalesPath}/{code}.asset";
        var locale  = AssetDatabase.LoadAssetAtPath<Locale>(path);
        if (locale == null)
        {
            locale = Locale.CreateLocale(new LocaleIdentifier(code));
            AssetDatabase.CreateAsset(locale, path);
        }
        LocalizationEditorSettings.AddLocale(locale);
        return locale;
    }

    static StringTableCollection CreateStringTableCollection(string name, Locale locale, Dictionary<string, string> entries)
    {
        var existing = LocalizationEditorSettings.GetStringTableCollection(name);
        if (existing != null)
        {
            Debug.Log($"[LocalizationSetup] Table '{name}' already exists – skipping creation.");
            return existing;
        }
        EnsureDirectory($"{TablesPath}/{name}");
        var collection = LocalizationEditorSettings.CreateStringTableCollection(name, $"{TablesPath}/{name}", new List<Locale> { locale });
        var table = collection.GetTable(locale.Identifier) as StringTable;
        if (table != null)
        {
            foreach (var kvp in entries) table.AddEntry(kvp.Key, kvp.Value);
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData);
        return collection;
    }

    // ─── LocalizedString factory ──────────────────────────────────────────────

    static LocalizedString MakeLS(StringTableCollection collection, string key)
    {
        var entry = collection.SharedData.GetEntry(key);
        if (entry == null)
        {
            Debug.LogWarning($"[LocalizationSetup] Key '{key}' not found in '{collection.TableCollectionName}'");
            return new LocalizedString();
        }
        var ls = new LocalizedString();
        ls.TableReference      = collection.SharedData.TableCollectionNameGuid;
        ls.TableEntryReference = entry.Id;
        return ls;
    }

    static List<LocalizedString> MakeLSList(StringTableCollection collection, params string[] keys)
    {
        var list = new List<LocalizedString>(keys.Length);
        foreach (var k in keys) list.Add(MakeLS(collection, k));
        return list;
    }

    // ─── Reflection helpers ───────────────────────────────────────────────────

    // Sets any field (including private/inherited) and marks the Unity Object dirty.
    static void SetField(UnityEngine.Object target, string fieldName, object value)
    {
        var type = target.GetType();
        FieldInfo field = null;
        for (var t = type; t != null && field == null; t = t.BaseType)
            field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        if (field == null) { Debug.LogWarning($"[LocalizationSetup] Field '{fieldName}' not found on '{type.Name}'"); return; }
        field.SetValue(target, value);
        EditorUtility.SetDirty(target);
    }

    // ─── Scene wiring ─────────────────────────────────────────────────────────

    static void WireSceneObjects(StringTableCollection ui, StringTableCollection puzzle)
    {
        // IdentifyKillerButton (4 LocalizedString fields)
        var btn = FindFirst<IdentifyKillerButton>();
        if (btn != null)
        {
            SetField(btn, "inactiveTooltipLocalized",        MakeLS(ui, "tooltip.inactive"));
            SetField(btn, "activeTooltipLocalized",          MakeLS(ui, "tooltip.active"));
            SetField(btn, "popupAllSuspectsLocalized",       MakeLS(ui, "popup.allSuspectsPlaced"));
            SetField(btn, "popupCluesNotRespectedLocalized", MakeLS(ui, "popup.cluesNotRespected"));
        }
        else Debug.LogWarning("[LocalizationSetup] IdentifyKillerButton not found.");

        // CursorManager
        var cursor = FindFirst<CursorManager>();
        if (cursor != null)
            SetField(cursor, "identifyTooltipLocalized", MakeLS(ui, "cursor.identifyPrompt"));
        else Debug.LogWarning("[LocalizationSetup] CursorManager not found.");

        // All TooltipUI instances
        foreach (var tip in FindAll<TooltipUI>())
            SetField(tip, "cluesHeaderLocalized", MakeLS(ui, "tooltip.cluesHeader"));

        // CluesTooltip per character
        WireCharacterClues(puzzle, "Carla",
            new[] { "clue.carla.0" },
            new[] { "clue.carla.tooltip.0" });
        WireCharacterClues(puzzle, "Bruno",
            new[] { "clue.bruno.0", "clue.bruno.1" },
            new[] { "clue.bruno.tooltip.0", "clue.bruno.tooltip.1" });
        WireCharacterClues(puzzle, "Adam",
            new[] { "clue.adam.0" },
            new[] { "clue.adam.tooltip.0" });

        // NameLabel on Victim
        var victimGO = GameObject.Find("Victim");
        if (victimGO != null)
        {
            var nl = victimGO.GetComponentInChildren<NameLabel>(true);
            if (nl != null) SetField(nl, "localizedName", MakeLS(puzzle, "name.victim"));
        }
        else Debug.LogWarning("[LocalizationSetup] 'Victim' not found.");

        // SuspectsLabel → LocalizeStringEvent + TMP binding
        var suspectsLabelGO = GameObject.Find("SuspectsLabel");
        if (suspectsLabelGO != null)
            AddLocalizeStringEvent(suspectsLabelGO, ui, "label.suspects");
        else
            Debug.LogWarning("[LocalizationSetup] 'SuspectsLabel' not found.");
    }

    static void WireCharacterClues(StringTableCollection puzzle, string charName, string[] firstKeys, string[] secondKeys)
    {
        var go = GameObject.Find(charName);
        if (go == null) { Debug.LogWarning($"[LocalizationSetup] '{charName}' not found."); return; }

        var tooltips = go.GetComponentsInChildren<CluesTooltip>(true);
        string[][] groups = { firstKeys, secondKeys };
        for (int i = 0; i < tooltips.Length && i < groups.Length; i++)
            SetField(tooltips[i], "lines", MakeLSList(puzzle, groups[i]));
    }

    static void AddLocalizeStringEvent(GameObject go, StringTableCollection collection, string key)
    {
        var lse = go.GetComponent<LocalizeStringEvent>() ?? go.AddComponent<LocalizeStringEvent>();

        // Set the string reference via public property
        var sr = lse.StringReference;
        sr.TableReference      = collection.SharedData.TableCollectionNameGuid;
        sr.TableEntryReference = collection.SharedData.GetEntry(key)?.Id ?? 0;
        EditorUtility.SetDirty(lse);

        // Wire UpdateString event to TMP.set_text via SerializedProperty
        var tmp = (UnityEngine.Object)go.GetComponent<TMPro.TextMeshProUGUI>()
                ?? go.GetComponent<TMPro.TextMeshPro>();
        if (tmp == null) return;

        var so = new SerializedObject(lse);
        // LocalizeStringEvent uses "m_UpdateString" as the backing field name
        var updateProp = so.FindProperty("m_UpdateString");
        if (updateProp == null) { Debug.LogWarning("[LocalizationSetup] m_UpdateString not found on LocalizeStringEvent."); so.ApplyModifiedProperties(); return; }

        var callsProp = updateProp
            .FindPropertyRelative("m_PersistentCalls")
            ?.FindPropertyRelative("m_Calls");
        if (callsProp == null) { so.ApplyModifiedProperties(); return; }

        if (callsProp.arraySize == 0)
        {
            callsProp.arraySize = 1;
            var call = callsProp.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = tmp;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                $"{tmp.GetType().FullName}, {tmp.GetType().Assembly.GetName().Name}";
            call.FindPropertyRelative("m_MethodName").stringValue  = "set_text";
            call.FindPropertyRelative("m_Mode").intValue           = 5; // PersistentListenerMode.String
            call.FindPropertyRelative("m_CallState").intValue      = 2; // RuntimeOnly
            var strArgProp = call.FindPropertyRelative("m_Arguments")
                              ?.FindPropertyRelative("m_StringArgument");
            if (strArgProp != null) strArgProp.stringValue = "";
        }
        so.ApplyModifiedProperties();
    }

    // ─── Utilities ────────────────────────────────────────────────────────────

    static T FindFirst<T>() where T : Component =>
        GameObject.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    static T[] FindAll<T>() where T : Component =>
        GameObject.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    // ─── Add pt-BR ───────────────────────────────────────────────────────────

    public static void AddPortuguese()
    {
        var ptLocale = CreateLocale("pt-BR");

        AddTranslationsToCollection("UI", ptLocale, new Dictionary<string, string>
        {
            ["tooltip.inactive"]        = "Você precisa colocar todos os suspeitos, objetos e a vítima no mapa antes de poder identificar o assassino",
            ["tooltip.active"]          = "Clique para identificar o assassino",
            ["popup.allSuspectsPlaced"] = "Todos os suspeitos precisam estar no tabuleiro!",
            ["popup.cluesNotRespected"] = "Algumas pistas não estão sendo respeitadas!",
            ["cursor.identifyPrompt"]   = "Quem é o assassino?\nEscolha com sabedoria. Não há volta atrás.",
            ["label.suspects"]          = "Suspeitos",
            ["tooltip.cluesHeader"]     = "<b>Pistas:</b>",
        });

        AddTranslationsToCollection("Puzzle", ptLocale, new Dictionary<string, string>
        {
            ["clue.carla.0"]         = "Carla era a única pessoa em sua linha e coluna",
            ["clue.carla.tooltip.0"] = "Não havia outra pessoa na mesma linha e coluna de Carla",
            ["clue.bruno.0"]         = "Bruno era a única pessoa em sua linha e coluna",
            ["clue.bruno.1"]         = "Bruno estava sozinho na sala",
            ["clue.bruno.tooltip.0"] = "Não havia outra pessoa na mesma linha e coluna de Bruno",
            ["clue.bruno.tooltip.1"] = "Bruno era a única pessoa na sala",
            ["clue.adam.0"]          = "Adam era a única pessoa em sua linha e coluna",
            ["clue.adam.tooltip.0"]  = "Não havia outra pessoa na mesma linha e coluna de Adam",
            ["name.victim"]          = "Vítima",
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] pt-BR translations added.");
    }

    static void AddTranslationsToCollection(string tableName, Locale locale, Dictionary<string, string> translations)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null) { Debug.LogWarning($"[LocalizationSetup] Table '{tableName}' not found."); return; }

        // Add a new StringTable for this locale if it doesn't exist yet
        var existing = collection.GetTable(locale.Identifier);
        StringTable table;
        if (existing == null)
        {
            table = collection.AddNewTable(locale.Identifier) as StringTable;
            if (table == null) { Debug.LogWarning($"[LocalizationSetup] Could not create table for '{locale.Identifier}' in '{tableName}'."); return; }
        }
        else
        {
            table = existing as StringTable;
        }

        foreach (var kvp in translations)
        {
            var entry = table.GetEntry(kvp.Key) ?? table.AddEntry(kvp.Key, kvp.Value);
            entry.Value = kvp.Value;
        }

        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(collection.SharedData);
    }
}
#endif
