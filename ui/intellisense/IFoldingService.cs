using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>App-wide singleton (ui-editor-services-guide.md §3.6). One fold region per CNL sentence, via IQueryRunner.RunFolds (bdd-ui-interactions.md §2.9).</summary>
public interface IFoldingService
{
    IEnumerable<FoldRegion> GetFolds(TSNode root);
}
