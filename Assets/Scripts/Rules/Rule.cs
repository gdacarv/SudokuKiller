using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

public enum ComparisonMode { LessThan, EqualTo, GreaterThan }
public enum PositionComparison { Less, LessOrEqual, Equal, GreaterOrEqual, Greater }

public abstract class Rule : ScriptableObject
{
    [FormerlySerializedAs("failureMessage")] [SerializeField] private LocalizedString killerFailureMessage;

    public abstract bool CanPlace(GridManager manager, Draggable target, int row, int col);

    public string GetFailureMessage(Draggable target = null)
    {
        if (target == null) return killerFailureMessage.GetLocalizedString();
        var nameLabel = target.GetComponent<NameLabel>();
        var name = nameLabel != null ? nameLabel.GetLocalizedName() : target.name;
        return killerFailureMessage.GetLocalizedString(name);
    }

protected static bool Compare(int count, ComparisonMode mode, int n) => mode switch
    {
        ComparisonMode.LessThan    => count < n,
        ComparisonMode.EqualTo     => count == n,
        ComparisonMode.GreaterThan => count > n,
        _                          => false,
    };
}
