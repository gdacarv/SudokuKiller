using UnityEngine;
using UnityEngine.Localization;

public abstract class KillerRule : ScriptableObject
{
    [SerializeField] private LocalizedString failureMessage;

    public abstract bool Evaluate(GridManager manager, Draggable suspect);

    public string GetFailureMessage(Draggable suspect = null)
    {
        if (suspect == null) return failureMessage.GetLocalizedString();
        var nameLabel = suspect.GetComponent<NameLabel>();
        var name = nameLabel != null ? nameLabel.GetLocalizedName() : suspect.name;
        return failureMessage.GetLocalizedString(name);
    }
}
