using UnityEngine;

public enum ComparisonMode { LessThan, EqualTo, GreaterThan }

public abstract class DraggableRule : ScriptableObject
{
    public abstract bool CanPlace(GridManager manager, Draggable draggable, int row, int col);

    protected static bool Compare(int count, ComparisonMode mode, int n)
    {
        return mode switch
        {
            ComparisonMode.LessThan => count < n,
            ComparisonMode.EqualTo => count == n,
            ComparisonMode.GreaterThan => count > n,
            _ => false,
        };
    }
}
