using UnityEngine;
using UnityEngine.Localization;

public abstract class KillerRule : ScriptableObject
{
    [SerializeField] private LocalizedString failureMessage;

    public abstract bool Evaluate(GridManager manager, Draggable suspect);

    public string GetFailureMessage(Draggable suspect = null)
    {
        var template = failureMessage.GetLocalizedString();
        if (suspect == null) return template;
        var nameLabel = suspect.GetComponent<NameLabel>();
        var name = nameLabel != null ? nameLabel.GetLocalizedName() : suspect.name;
        return string.Format(template, name);
    }
}
