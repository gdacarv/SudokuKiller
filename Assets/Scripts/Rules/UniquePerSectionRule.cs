using UnityEngine;

[CreateAssetMenu(menuName = "Sudoku/Rules/Unique Per Section")]
public class UniquePerSectionRule : DraggableRule
{
    public string tag;

    public override bool CanPlace(GridManager manager, Draggable draggable, int row, int col)
    {
        int section = manager.GetSection(row, col);
        if (section == -1) return true;
        return !manager.HasTagInSection(section, tag, draggable);
    }
}
