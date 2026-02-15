public class BlockedCellMarker : GridEntityMarker
{
    public bool hideGridCell = false;

    public override void ApplyRule(GridManager manager)
    {
        manager.MarkBlocked(row, col);
        if (hideGridCell) manager.HideGridCell(row, col);
    }
}
