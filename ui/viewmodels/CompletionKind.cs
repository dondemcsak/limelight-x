namespace LimelightX.UI.ViewModels;

/// <summary>
/// Ranking category for a CompletionItem (ui-intellisense-engine-spec.md
/// §5.3: "Variables rank above pronouns, verbs rank above keywords, prompt
/// templates rank lowest"). Declared in ascending rank order so sorting by
/// the enum's own integer value directly produces the required order.
/// </summary>
public enum CompletionKind
{
    Variable,
    Pronoun,
    Verb,
    Keyword,
    PromptTemplate,
}
