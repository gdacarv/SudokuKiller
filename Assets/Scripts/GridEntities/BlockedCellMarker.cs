public class BlockedCellMarker : GridEntityMarker
{
    public override void ApplyRule(GridManager manager) => manager.MarkBlocked(row, col);
}
