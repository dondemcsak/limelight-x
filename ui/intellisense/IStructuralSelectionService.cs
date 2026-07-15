namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton. Grows a selection to its smallest strictly-larger
/// enclosing CST node, one grammar-meaningful step per call
/// (bdd-ui-interactions.md §2.10). Closes the gap found while writing the
/// BDD-tests-first pass: none of the other five services (IParserHost/
/// IQueryRunner/ICompletionService/IDiagnosticService/IHoverService/
/// IFoldingService) own CST parent/child navigation.
/// </summary>
public interface IStructuralSelectionService
{
    (int Start, int End) ExpandSelection(TSNode root, int startByte, int endByte);
}
