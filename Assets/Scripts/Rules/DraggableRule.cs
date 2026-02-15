using UnityEngine;

public abstract class DraggableRule : ScriptableObject
{
    public abstract bool CanPlace(GridManager manager, Draggable draggable, int row, int col);
}
