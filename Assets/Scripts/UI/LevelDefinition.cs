using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "SudoKillers/Level Definition", fileName = "LevelDefinition")]
public class LevelDefinition : ScriptableObject
{
    [SerializeField] public Sprite thumbnail;
    [SerializeField] public LocalizedString levelName;
    [SerializeField] public string sceneName;
}
