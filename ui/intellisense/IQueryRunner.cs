namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton: loads and runs the highlights/folds/injections .scm
/// queries (ui/queries/, spec/parsing/tree-sitter-integration.md §6) against
/// a caller-supplied CST. Stateless per call beyond its cached compiled
/// queries - shared across all tabs, unlike IParserHost.
/// </summary>
public interface IQueryRunner : IDisposable
{
    IEnumerable<QueryMatch> RunHighlights(TSNode root);

    IEnumerable<QueryMatch> RunFolds(TSNode root);

    IEnumerable<QueryMatch> RunInjections(TSNode root);
}
