using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. App-wide singleton, sharing the
/// app's one IQueryRunner (folds.scm has no keyword-boundary bug, unlike
/// highlights.scm - see spec/parsing/tree-sitter-runtime-build-guide.md §6 -
/// so this needed no fix before landing).
/// </summary>
public sealed class FoldingService(IQueryRunner queryRunner) : IFoldingService
{
    public IEnumerable<FoldRegion> GetFolds(TSNode root) =>
        queryRunner.RunFolds(root).Select(m => new FoldRegion(m.StartByte, m.EndByte));
}
