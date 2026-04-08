#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VariableSyncRule
{
    [Tooltip("Name of the StringTableCollection to scan (e.g. \"Puzzle\")")]
    public string tableCollectionName;

    [Tooltip("Key prefix to match (e.g. \"name.\"). Variable name = key minus this prefix.")]
    public string keyPrefix;

    [Tooltip("Group name registered in PersistentVariablesSource (e.g. \"char\" → {char.adam})")]
    public string groupName;

    [Tooltip("Path to the VariablesGroupAsset (created if missing), relative to project root")]
    public string groupAssetPath;
}

[CreateAssetMenu(fileName = "VariableSyncConfig", menuName = "Localization/Variable Sync Config")]
public class VariableSyncConfig : ScriptableObject
{
    public List<VariableSyncRule> rules = new List<VariableSyncRule>();
}
#endif
