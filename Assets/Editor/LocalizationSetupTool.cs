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
using UnityEngine.Localization.SmartFormat.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
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
            ["clue.carla.0"]  = "{char.carla} was the only person in his row and column",
            ["clue.bruno.0"]  = "{char.bruno} was the only person in his row and column",
            ["clue.bruno.1"]  = "{char.bruno} was alone in the room",
            ["clue.adam.0"]   = "{char.adam} was the only person in his row and column",
            ["name.victim"]   = "Victim",
            ["name.adam"]     = "Adam",
            ["name.bruno"]    = "Bruno",
            ["name.carla"]    = "Carla",
        });

        // Enable Smart Strings on clue entries
        var enTable = puzzle.GetTable(enLocale.Identifier) as StringTable;
        if (enTable != null) SetSmartOnClueEntries(enTable);

        AssetDatabase.SaveAssets();
        WireSceneObjects(ui, puzzle);
        SyncVariableGroupsMenuItem();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] Done! Check Console for any warnings.");
    }

    // ─── Smart Name Variables ──────────────────────────────────────────────────

    public static void SetupSmartNames()
    {
        var puzzle = LocalizationEditorSettings.GetStringTableCollection("Puzzle");
        if (puzzle == null) { Debug.LogError("[LocalizationSetup] Puzzle table not found. Run Setup first."); return; }

        var enTable = puzzle.GetTable(new LocaleIdentifier("en")) as StringTable;
        if (enTable != null)
        {
            AddOrUpdateEntry(enTable, "name.adam",  "Adam");
            AddOrUpdateEntry(enTable, "name.bruno", "Bruno");
            AddOrUpdateEntry(enTable, "name.carla", "Carla");

            UpdateEntryAsSmart(enTable, "clue.adam.0",  "{char.adam} was the only person in his row and column");
            UpdateEntryAsSmart(enTable, "clue.bruno.0", "{char.bruno} was the only person in his row and column");
            UpdateEntryAsSmart(enTable, "clue.bruno.1", "{char.bruno} was alone in the room");
            UpdateEntryAsSmart(enTable, "clue.carla.0", "{char.carla} was the only person in his row and column");
            EditorUtility.SetDirty(enTable);
        }

        var ptTable = puzzle.GetTable(new LocaleIdentifier("pt-BR")) as StringTable;
        if (ptTable != null)
        {
            AddOrUpdateEntry(ptTable, "name.adam",  "Adam");
            AddOrUpdateEntry(ptTable, "name.bruno", "Bruno");
            AddOrUpdateEntry(ptTable, "name.carla", "Carla");

            UpdateEntryAsSmart(ptTable, "clue.adam.0",  "{char.adam} era a única pessoa em sua linha e coluna");
            UpdateEntryAsSmart(ptTable, "clue.bruno.0", "{char.bruno} era a única pessoa em sua linha e coluna");
            UpdateEntryAsSmart(ptTable, "clue.bruno.1", "{char.bruno} estava sozinho na sala");
            UpdateEntryAsSmart(ptTable, "clue.carla.0", "{char.carla} era a única pessoa em sua linha e coluna");
            EditorUtility.SetDirty(ptTable);
        }

        EditorUtility.SetDirty(puzzle.SharedData);
        SyncVariableGroupsMenuItem();

        foreach (var charName in new[] { "Adam", "Bruno", "Carla" })
        {
            var go = GameObject.Find(charName);
            if (go == null) { Debug.LogWarning($"[LocalizationSetup] '{charName}' not found."); continue; }
            var nl = go.GetComponentInChildren<NameLabel>(true);
            if (nl != null)
                SetField(nl, "localizedName", MakeLS(puzzle, $"name.{charName.ToLower()}"));
            else
                Debug.LogWarning($"[LocalizationSetup] NameLabel not found on '{charName}'.");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] Smart names setup complete!");
    }

    public static void CleanupTooltipKeys()
    {
        var puzzle = LocalizationEditorSettings.GetStringTableCollection("Puzzle");
        if (puzzle == null) { Debug.LogError("[LocalizationSetup] Puzzle table not found."); return; }

        var keysToRemove = new[]
        {
            "clue.adam.tooltip.0",
            "clue.bruno.tooltip.0",
            "clue.bruno.tooltip.1",
            "clue.carla.tooltip.0",
        };

        foreach (var key in keysToRemove)
        {
            var sharedEntry = puzzle.SharedData.GetEntry(key);
            if (sharedEntry == null) { Debug.Log($"[LocalizationSetup] Key '{key}' not found, skipping."); continue; }

            long id = sharedEntry.Id;
            foreach (var tableObj in puzzle.StringTables)
            {
                var st = tableObj as StringTable;
                if (st == null) continue;
                if (st.GetEntry(id) != null)
                {
                    st.RemoveEntry(id);
                    EditorUtility.SetDirty(st);
                }
            }
            puzzle.SharedData.RemoveKey(id);
            EditorUtility.SetDirty(puzzle.SharedData);
            Debug.Log($"[LocalizationSetup] Removed key '{key}'.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] Tooltip keys cleanup complete.");
    }

    [MenuItem("Localization/Sync Variable Groups")]
    public static void SyncVariableGroupsMenuItem()
    {
        const string configPath = "Assets/Localization/VariableSyncConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<VariableSyncConfig>(configPath);
        if (config == null)
        {
            Debug.LogError($"[VariableSync] Config not found at '{configPath}'. " +
                "Create one via Assets > Create > Localization > Variable Sync Config.");
            return;
        }
        SyncVariableGroups(config);
    }

    static void SyncVariableGroups(VariableSyncConfig config)
    {
        var source = LocalizationSettings.StringDatabase.SmartFormatter
            .GetSourceExtension<PersistentVariablesSource>();
        if (source == null)
        {
            Debug.LogError("[VariableSync] PersistentVariablesSource not found in SmartFormatter.");
            return;
        }

        foreach (var rule in config.rules)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(rule.tableCollectionName);
            if (collection == null)
            {
                Debug.LogWarning($"[VariableSync] Table '{rule.tableCollectionName}' not found, skipping rule.");
                continue;
            }

            var group = AssetDatabase.LoadAssetAtPath<VariablesGroupAsset>(rule.groupAssetPath);
            if (group == null)
            {
                EnsureDirectory(Path.GetDirectoryName(rule.groupAssetPath));
                group = ScriptableObject.CreateInstance<VariablesGroupAsset>();
                AssetDatabase.CreateAsset(group, rule.groupAssetPath);
            }

            // Clear and rebuild via SerializedObject to bypass type validation
            var so = new SerializedObject(group);
            var varsProp = so.FindProperty("m_Variables");
            varsProp.ClearArray();

            int count = 0;
            foreach (var entry in collection.SharedData.Entries)
            {
                if (!entry.Key.StartsWith(rule.keyPrefix)) continue;
                string varName = entry.Key.Substring(rule.keyPrefix.Length);
                if (string.IsNullOrEmpty(varName)) continue;
                varsProp.InsertArrayElementAtIndex(count);
                var elem = varsProp.GetArrayElementAtIndex(count);
                elem.FindPropertyRelative("name").stringValue = varName;
                elem.FindPropertyRelative("variable").managedReferenceValue = MakeLS(collection, entry.Key);
                count++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(group);

            if (source.ContainsKey(rule.groupName)) source.Remove(rule.groupName);
            source.Add(rule.groupName, group);

            Debug.Log($"[VariableSync] Group '{rule.groupName}': synced {count} variables " +
                $"from '{rule.tableCollectionName}' (prefix '{rule.keyPrefix}')");
        }

        EditorUtility.SetDirty(LocalizationEditorSettings.ActiveLocalizationSettings);
        AssetDatabase.SaveAssets();
        Debug.Log("[VariableSync] Sync complete.");
    }

    static void SetSmartOnClueEntries(StringTable table)
    {
        var clueKeys = new[] { "clue.carla.0", "clue.bruno.0", "clue.bruno.1", "clue.adam.0" };
        foreach (var key in clueKeys)
        {
            var entry = table.GetEntry(key);
            if (entry != null) entry.IsSmart = true;
        }
        EditorUtility.SetDirty(table);
    }

    internal static void AddOrUpdateEntry(StringTable table, string key, string value)
    {
        var entry = table.GetEntry(key);
        if (entry == null)
            table.AddEntry(key, value);
        else
            entry.Value = value;
    }

    static void UpdateEntryAsSmart(StringTable table, string key, string value)
    {
        var entry = table.GetEntry(key);
        if (entry == null) { Debug.LogWarning($"[LocalizationSetup] Entry '{key}' not found."); return; }
        entry.Value = value;
        entry.IsSmart = true;
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

    internal static LocalizedString MakeLS(StringTableCollection collection, string key)
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

    internal static void SetField(UnityEngine.Object target, string fieldName, object value)
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
        var btn = FindFirst<IdentifyKillerButton>();
        if (btn != null)
        {
            SetField(btn, "inactiveTooltipLocalized",        MakeLS(ui, "tooltip.inactive"));
            SetField(btn, "activeTooltipLocalized",          MakeLS(ui, "tooltip.active"));
            SetField(btn, "popupAllSuspectsLocalized",       MakeLS(ui, "popup.allSuspectsPlaced"));
            SetField(btn, "popupCluesNotRespectedLocalized", MakeLS(ui, "popup.cluesNotRespected"));
        }
        else Debug.LogWarning("[LocalizationSetup] IdentifyKillerButton not found.");

        var cursor = FindFirst<CursorManager>();
        if (cursor != null)
            SetField(cursor, "identifyTooltipLocalized", MakeLS(ui, "cursor.identifyPrompt"));
        else Debug.LogWarning("[LocalizationSetup] CursorManager not found.");

        foreach (var tip in FindAll<TooltipUI>())
            SetField(tip, "cluesHeaderLocalized", MakeLS(ui, "tooltip.cluesHeader"));

        WireCharacterClues(puzzle, "Carla", new[] { "clue.carla.0" });
        WireCharacterClues(puzzle, "Bruno", new[] { "clue.bruno.0", "clue.bruno.1" });
        WireCharacterClues(puzzle, "Adam",  new[] { "clue.adam.0" });

        var victimGO = GameObject.Find("Victim");
        if (victimGO != null)
        {
            var nl = victimGO.GetComponentInChildren<NameLabel>(true);
            if (nl != null) SetField(nl, "localizedName", MakeLS(puzzle, "name.victim"));
        }
        else Debug.LogWarning("[LocalizationSetup] 'Victim' not found.");

        foreach (var charName in new[] { "Adam", "Bruno", "Carla" })
        {
            var go = GameObject.Find(charName);
            if (go == null) { Debug.LogWarning($"[LocalizationSetup] '{charName}' not found."); continue; }
            var nl = go.GetComponentInChildren<NameLabel>(true);
            if (nl != null)
                SetField(nl, "localizedName", MakeLS(puzzle, $"name.{charName.ToLower()}"));
        }

        var suspectsLabelGO = GameObject.Find("SuspectsLabel");
        if (suspectsLabelGO != null)
            AddLocalizeStringEvent(suspectsLabelGO, ui, "label.suspects");
        else
            Debug.LogWarning("[LocalizationSetup] 'SuspectsLabel' not found.");
    }

    static void WireCharacterClues(StringTableCollection puzzle, string charName, string[] keys)
    {
        var go = GameObject.Find(charName);
        if (go == null) { Debug.LogWarning($"[LocalizationSetup] '{charName}' not found."); return; }
        var tooltip = go.GetComponentInChildren<CluesTooltip>(true);
        if (tooltip != null)
            SetField(tooltip, "lines", MakeLSList(puzzle, keys));
        else
            Debug.LogWarning($"[LocalizationSetup] CluesTooltip not found on '{charName}'.");
    }

    static void AddLocalizeStringEvent(GameObject go, StringTableCollection collection, string key)
    {
        var lse = go.GetComponent<LocalizeStringEvent>() ?? go.AddComponent<LocalizeStringEvent>();

        var sr = lse.StringReference;
        sr.TableReference      = collection.SharedData.TableCollectionNameGuid;
        sr.TableEntryReference = collection.SharedData.GetEntry(key)?.Id ?? 0;
        EditorUtility.SetDirty(lse);

        var tmp = (UnityEngine.Object)go.GetComponent<TMPro.TextMeshProUGUI>()
                ?? go.GetComponent<TMPro.TextMeshPro>();
        if (tmp == null) return;

        var so = new SerializedObject(lse);
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
            call.FindPropertyRelative("m_Mode").intValue           = 5;
            call.FindPropertyRelative("m_CallState").intValue      = 2;
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
            ["clue.carla.0"]  = "{char.carla} era a única pessoa em sua linha e coluna",
            ["clue.bruno.0"]  = "{char.bruno} era a única pessoa em sua linha e coluna",
            ["clue.bruno.1"]  = "{char.bruno} estava sozinho na sala",
            ["clue.adam.0"]   = "{char.adam} era a única pessoa em sua linha e coluna",
            ["name.victim"]   = "Vítima",
            ["name.adam"]     = "Adam",
            ["name.bruno"]    = "Bruno",
            ["name.carla"]    = "Carla",
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationSetup] pt-BR translations added.");
    }

    static void AddTranslationsToCollection(string tableName, Locale locale, Dictionary<string, string> translations)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null) { Debug.LogWarning($"[LocalizationSetup] Table '{tableName}' not found."); return; }

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
            if (kvp.Key.StartsWith("clue."))
                entry.IsSmart = true;
        }

        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(collection.SharedData);
    }
}
#endif
