#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

public static class AddSuspectTool
{
    // ─── Layout ───────────────────────────────────────────────────────────────
    const float LeftColumnX  = -6.0f;
    const float RightColumnX = -5.04f;
    const float TopRowY      = 3.12f;
    const float RowSpacing   = 0.96f;
    // ─── Sprite Pools (path, slice name) ─────────────────────────────────────
    // Each entry specifies the exact sprite slice to use from the sprite sheet.
    static readonly (string path, string slice)[] MaleSprites = {
        ("Assets/Sprites/Characters/Adam_idle_48x48.png",      "Adam_idle_48x48_3"),
        ("Assets/Sprites/Characters/Alex_reading_48x48.png",   "Alex_reading_48x48_0"),
        ("Assets/Sprites/Characters/Ash_idle_anim_48x48.png",  "Ash_idle_anim_48x48_18"),
        ("Assets/Sprites/Characters/Bob_idle_anim_48x48.png",  "Bob_idle_anim_48x48_18"),
        ("Assets/Sprites/Characters/Doctor_1_48x48.png",       "Doctor_1_48x48_22"),
        ("Assets/Sprites/Characters/Fishmonger_1_48x48.png",   "Fishmonger_1_48x48_22"),
        ("Assets/Sprites/Characters/Old_man_Josh_48x48.png",   "Old_man_Josh_48x48_46"),
        ("Assets/Sprites/Characters/Pier_48x48.png",           "Pier_48x48_22"),
        ("Assets/Sprites/Characters/Rob_48x48.png",            "Rob_48x48_22"),
        ("Assets/Sprites/Characters/Roki_48x48.png",           "Roki_48x48_22"),
        ("Assets/Sprites/Characters/Samuel_48x48.png",         "Samuel_48x48_22"),
    };

    static readonly (string path, string slice)[] FemaleSprites = {
        ("Assets/Sprites/Characters/Amelia_phone_48x48.png",       "Amelia_phone_48x48_5"),
        ("Assets/Sprites/Characters/Cleaner_girl_48x48.png",       "Cleaner_girl_48x48_22"),
        ("Assets/Sprites/Characters/Doctor_2_48x48.png",           "Doctor_2_48x48_22"),
        ("Assets/Sprites/Characters/Fishmonger_2_48x48.png",       "Fishmonger_2_48x48_22"),
        ("Assets/Sprites/Characters/Kid_Abby_48x48.png",           "Kid_Abby_48x48_22"),
        ("Assets/Sprites/Characters/Kid_Karen_48x48.png",           "Kid_Karen_48x48_22"),
        ("Assets/Sprites/Characters/Lucy_48x48.png",               "Lucy_48x48_22"),
        ("Assets/Sprites/Characters/Molly_48x48.png",              "Molly_48x48_22"),
        ("Assets/Sprites/Characters/Old_woman_Jenny_48x48.png",    "Old_woman_Jenny_48x48_46"),
    };

    // ─── Name Lists ───────────────────────────────────────────────────────────
    static readonly Dictionary<char, string[]> MaleNames = new()
    {
        { 'A', new[] { "Aaron", "Adrian", "Alan" } },
        { 'B', new[] { "Benjamin", "Blake", "Boris" } },
        { 'C', new[] { "Carlos", "Charles", "Cole" } },
        { 'D', new[] { "Daniel", "Derek", "Dominic" } },
        { 'E', new[] { "Ethan", "Elliot", "Ernest" } },
        { 'F', new[] { "Felix", "Fernando", "Frank" } },
        { 'G', new[] { "Gabriel", "Garrett", "George" } },
        { 'H', new[] { "Henry", "Hugo", "Harold" } },
        { 'I', new[] { "Ian", "Ivan", "Isaiah" } },
        { 'J', new[] { "James", "Jonas", "Julian" } },
        { 'K', new[] { "Karl", "Kevin", "Kyle" } },
        { 'L', new[] { "Leo", "Liam", "Lucas" } },
        { 'M', new[] { "Marcus", "Martin", "Mason" } },
        { 'N', new[] { "Nathan", "Neil", "Nicolas" } },
        { 'O', new[] { "Oliver", "Omar", "Oscar" } },
        { 'P', new[] { "Patrick", "Paul", "Peter" } },
        { 'Q', new[] { "Quentin", "Quinn" } },
        { 'R', new[] { "Rafael", "Raymond", "Robert" } },
        { 'S', new[] { "Samuel", "Sebastian", "Simon" } },
        { 'T', new[] { "Thomas", "Timothy", "Tyler" } },
        { 'U', new[] { "Ulric", "Uriah" } },
        { 'V', new[] { "Victor", "Vincent", "Virgil" } },
        { 'W', new[] { "Walter", "Warren", "William" } },
        { 'X', new[] { "Xavier", "Xander" } },
        { 'Y', new[] { "Yannick", "Yusuf" } },
        { 'Z', new[] { "Zachary", "Zane" } },
    };

    static readonly Dictionary<char, string[]> FemaleNames = new()
    {
        { 'A', new[] { "Alice", "Amber", "Amelia" } },
        { 'B', new[] { "Beatrice", "Bella", "Bridget" } },
        { 'C', new[] { "Camille", "Clara", "Cora" } },
        { 'D', new[] { "Diana", "Dorothy", "Daphne" } },
        { 'E', new[] { "Elena", "Elise", "Emma" } },
        { 'F', new[] { "Fiona", "Frances", "Freya" } },
        { 'G', new[] { "Grace", "Greta", "Gloria" } },
        { 'H', new[] { "Hannah", "Harper", "Hazel" } },
        { 'I', new[] { "Iris", "Irene", "Ingrid" } },
        { 'J', new[] { "Jane", "Julia", "June" } },
        { 'K', new[] { "Kate", "Karen", "Kira" } },
        { 'L', new[] { "Laura", "Leila", "Lily" } },
        { 'M', new[] { "Maria", "Martha", "Maya" } },
        { 'N', new[] { "Nadia", "Nina", "Nora" } },
        { 'O', new[] { "Olivia", "Ophelia" } },
        { 'P', new[] { "Patricia", "Penelope", "Petra" } },
        { 'Q', new[] { "Quinn", "Quincy" } },
        { 'R', new[] { "Rachel", "Rebecca", "Rosa" } },
        { 'S', new[] { "Sara", "Sofia", "Sylvia" } },
        { 'T', new[] { "Tara", "Teresa", "Tina" } },
        { 'U', new[] { "Uma", "Ursula" } },
        { 'V', new[] { "Valeria", "Vera", "Violet" } },
        { 'W', new[] { "Wanda", "Wendy", "Wilma" } },
        { 'X', new[] { "Xena", "Xiomara" } },
        { 'Y', new[] { "Yasmine", "Yvette" } },
        { 'Z', new[] { "Zoe", "Zelda", "Zara" } },
    };

    // ─── Menu Item ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Add Suspect")]
    public static void AddSuspect()
    {
        // ── Validate scene prerequisites ──────────────────────────────────────
        var draggablesGO = GameObject.Find("Draggables");
        if (draggablesGO == null)
        {
            EditorUtility.DisplayDialog("Add Suspect", "Could not find 'Draggables' GameObject in the scene.", "OK");
            return;
        }

        var puzzle = LocalizationEditorSettings.GetStringTableCollection("Puzzle");
        if (puzzle == null)
        {
            EditorUtility.DisplayDialog("Add Suspect", "Puzzle localization table not found. Run Setup first.", "OK");
            return;
        }
        var names = LocalizationEditorSettings.GetStringTableCollection("Names");
        if (names == null)
        {
            EditorUtility.DisplayDialog("Add Suspect", "Names localization table not found. Run Setup first.", "OK");
            return;
        }

        // ── Determine next available letter ───────────────────────────────────
        var existingPersons = GameObject.FindGameObjectsWithTag("Person");
        var usedLetters = new HashSet<char>(
            existingPersons.Select(g => char.ToUpper(g.name[0]))
        );

        char nextLetter = '\0';
        for (char c = 'A'; c <= 'Z'; c++)
        {
            if (!usedLetters.Contains(c)) { nextLetter = c; break; }
        }

        if (nextLetter == '\0')
        {
            EditorUtility.DisplayDialog("Add Suspect", "All 26 letters are already used!", "OK");
            return;
        }

        // ── Pick gender and name ───────────────────────────────────────────────
        bool isMale = Random.value < 0.5f;
        var nameDict = isMale ? MaleNames : FemaleNames;
        if (!nameDict.TryGetValue(nextLetter, out var nameCandidates) || nameCandidates.Length == 0)
        {
            // Fallback to opposite gender
            isMale = !isMale;
            nameDict = isMale ? MaleNames : FemaleNames;
            nameCandidates = nameDict[nextLetter];
        }
        string chosenName = nameCandidates[Random.Range(0, nameCandidates.Length)];

        // ── Calculate position ─────────────────────────────────────────────────
        int index = existingPersons.Length;
        int col   = index % 2;
        int row   = index / 2;
        float localX = col == 0 ? LeftColumnX : RightColumnX;
        float localY = TopRowY - (row * RowSpacing);
        Vector3 localPos = new(localX, localY, 0f);
        Vector3 worldPos = draggablesGO.transform.position + localPos;

        // ── Begin Undo group ───────────────────────────────────────────────────
        Undo.SetCurrentGroupName($"Add Suspect '{chosenName}'");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Instantiate prefab ────────────────────────────────────────────────
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Person.prefab");
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Add Suspect", "Person prefab not found at Assets/Prefabs/Person.prefab", "OK");
            return;
        }

        var newGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(newGO, $"Add Suspect '{chosenName}'");

        newGO.name = chosenName;
        Undo.SetTransformParent(newGO.transform, draggablesGO.transform, $"Parent Suspect '{chosenName}'");
        newGO.transform.localPosition = localPos;

        // ── SolutionPosition.uiPosition ───────────────────────────────────────
        var solutionPos = newGO.GetComponent<SolutionPosition>();
        if (solutionPos != null)
        {
            Undo.RecordObject(solutionPos, "Set UI Position");
            solutionPos.uiPosition = worldPos;
            EditorUtility.SetDirty(solutionPos);
        }

        // ── Assign sprite ─────────────────────────────────────────────────────
        var usedTextures = new HashSet<Texture2D>(
            existingPersons
                .Select(g => g.GetComponent<SpriteRenderer>()?.sprite?.texture)
                .Where(t => t != null)
        );

        var spritePool = isMale ? MaleSprites : FemaleSprites;
        var available  = spritePool
            .Select(e => LoadSpriteSlice(e.path, e.slice))
            .Where(s => s != null && !usedTextures.Contains(s.texture))
            .ToList();

        if (available.Count == 0)
        {
            // Try opposite gender pool
            var fallbackPool = isMale ? FemaleSprites : MaleSprites;
            available = fallbackPool
                .Select(e => LoadSpriteSlice(e.path, e.slice))
                .Where(s => s != null && !usedTextures.Contains(s.texture))
                .ToList();
        }

        if (available.Count > 0)
        {
            var sr = newGO.GetComponent<SpriteRenderer>();
            Undo.RecordObject(sr, "Assign Sprite");
            sr.sprite = available[Random.Range(0, available.Count)];
            EditorUtility.SetDirty(sr);
        }
        else
        {
            Debug.LogWarning($"[AddSuspect] No unused sprites available — keeping prefab default.");
        }

        // ── Wire scene references ─────────────────────────────────────────────
        var gridManager   = Object.FindFirstObjectByType<GridManager>();
        var inputProvider = Object.FindFirstObjectByType<DragInputProvider>();
        var tooltipUI     = Object.FindFirstObjectByType<TooltipUI>();

        var draggable = newGO.GetComponent<Draggable>();
        if (draggable != null)
        {
            Undo.RecordObject(draggable, "Wire Draggable");
            draggable.gridManager   = gridManager;
            draggable.inputProvider = inputProvider;
            EditorUtility.SetDirty(draggable);
        }

        var cluesTooltip = newGO.GetComponent<CluesTooltip>();
        if (cluesTooltip != null)
        {
            Undo.RecordObject(cluesTooltip, "Wire CluesTooltip");
            cluesTooltip.inputProvider = inputProvider;
            cluesTooltip.tooltipUI     = tooltipUI;
            EditorUtility.SetDirty(cluesTooltip);
        }

        // ── Localization: add name.{key} entry to Names table ────────────────
        string locKey = $"name.{chosenName.ToLower()}";

        var enNamesTable = names.GetTable(new LocaleIdentifier("en")) as StringTable;
        if (enNamesTable != null)
        {
            Undo.RecordObject(enNamesTable, "Add Localization Entry");
            LocalizationSetupTool.AddOrUpdateEntry(enNamesTable, locKey, chosenName);
            EditorUtility.SetDirty(enNamesTable);
        }

        var ptNamesTable = names.GetTable(new LocaleIdentifier("pt-BR")) as StringTable;
        if (ptNamesTable != null)
        {
            Undo.RecordObject(ptNamesTable, "Add Localization Entry pt-BR");
            LocalizationSetupTool.AddOrUpdateEntry(ptNamesTable, locKey, chosenName);
            EditorUtility.SetDirty(ptNamesTable);
        }

        EditorUtility.SetDirty(names.SharedData);

        // ── Wire NameLabel.localizedName ──────────────────────────────────────
        var nameLabel = newGO.GetComponentInChildren<NameLabel>(true);
        if (nameLabel != null)
        {
            LocalizationSetupTool.SetField(nameLabel, "localizedName",
                LocalizationSetupTool.MakeLS(names, locKey));
        }

        // ── Sync variable groups (rebuilds CharacterNames.asset) ──────────────
        LocalizationSetupTool.SyncVariableGroupsMenuItem();

        // ── Save ──────────────────────────────────────────────────────────────
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Undo.CollapseUndoOperations(undoGroup);

        // ── Select new object ─────────────────────────────────────────────────
        Selection.activeGameObject = newGO;

        Debug.Log($"[AddSuspect] Created '{chosenName}' (letter '{nextLetter}', index {index}), " +
                  $"locKey='{locKey}', worldPos={worldPos}, gender={(isMale ? "male" : "female")}");
    }

    // Returns the named sprite slice from a multi-sprite texture asset.
    static Sprite LoadSpriteSlice(string path, string sliceName)
    {
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        return allAssets.OfType<Sprite>().FirstOrDefault(s => s.name == sliceName);
    }
}
#endif
