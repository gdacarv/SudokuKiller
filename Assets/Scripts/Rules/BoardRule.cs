using UnityEngine;

public abstract class BoardRule : ScriptableObject
{
    public abstract bool CanPlace(GridManager manager, Draggable incoming, int row, int col);
}
