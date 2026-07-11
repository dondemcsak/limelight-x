using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

public class EditorViewModelTests
{
    /// <summary>Never throws (unlike the real ParserHost stub) - returns a fixed default TSNode that the other fakes below ignore anyway.</summary>
    private sealed class FakeParserHost : IParserHost
    {
        public int ParseCallCount { get; private set; }

        public string? LastParsedText { get; private set; }

        public TSNode Parse(string text)
        {
            ParseCallCount++;
            LastParsedText = text;
            return default;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeCompletionService : ICompletionService
    {
        public IReadOnlyList<CompletionItem> ItemsToReturn { get; set; } = [];

        public IEnumerable<CompletionItem> GetCompletions(string text, TSNode root, int cursorByte) => ItemsToReturn;
    }

    private sealed class FakeDiagnosticService : IDiagnosticService
    {
        public IReadOnlyList<LocalDiagnostic> DiagnosticsToReturn { get; set; } = [];

        public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root) => DiagnosticsToReturn;
    }

    private sealed class FakeHoverService : IHoverService
    {
        public HoverInfo? HoverToReturn { get; set; }

        public HoverInfo? GetHover(string text, TSNode root, int cursorByte) => HoverToReturn;
    }

    private sealed class FakeFoldingService : IFoldingService
    {
        public IReadOnlyList<FoldRegion> FoldsToReturn { get; set; } = [];

        public IEnumerable<FoldRegion> GetFolds(TSNode root) => FoldsToReturn;
    }

    private sealed class FakeStructuralSelectionService : IStructuralSelectionService
    {
        public (int Start, int End) RangeToReturn { get; set; }

        public (int Start, int End) ExpandSelection(TSNode root, int startByte, int endByte) => RangeToReturn;
    }

    private sealed class FakeOutlineService : IOutlineService
    {
        public IReadOnlyList<OutlineItem> ItemsToReturn { get; set; } = [];

        public IEnumerable<OutlineItem> GetOutline(string text, TSNode root) => ItemsToReturn;
    }

    private sealed class FakeAutoPairService : IAutoPairService
    {
        public bool ResultToReturn { get; set; }

        public bool CanAutoClose(string text, TSNode root, int cursorByte, string opener) => ResultToReturn;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public (int Start, int End)? ResultToReturn { get; set; }

        public (int Start, int End)? FindDefinition(string text, TSNode root, int cursorByte) => ResultToReturn;
    }

    /// <summary>
    /// Constructs a real EditorViewModel with fakes for every collaborator,
    /// defaulted so each test only names the ones it cares about.
    /// EditorViewModel no longer takes IPipelineService/IEventStreamService -
    /// it never calls the backend on its own (cnl-editor-architecture.md §5);
    /// the backend is reached only via RunRequested/ExplainRequested, wired
    /// externally by CnlTabViewModel, which these unit tests don't exercise.
    /// </summary>
    private static EditorViewModel CreateViewModel(
        IExecutionLockService? executionLock = null,
        IParserHost? parserHost = null,
        ICompletionService? completionService = null,
        IDiagnosticService? diagnosticService = null,
        IHoverService? hoverService = null,
        IFoldingService? foldingService = null,
        IStructuralSelectionService? structuralSelectionService = null,
        IOutlineService? outlineService = null,
        IAutoPairService? autoPairService = null,
        INavigationService? navigationService = null) =>
        new(
            executionLock ?? new ExecutionLockService(),
            parserHost ?? new FakeParserHost(),
            completionService ?? new FakeCompletionService(),
            diagnosticService ?? new FakeDiagnosticService(),
            hoverService ?? new FakeHoverService(),
            foldingService ?? new FakeFoldingService(),
            structuralSelectionService ?? new FakeStructuralSelectionService(),
            outlineService ?? new FakeOutlineService(),
            autoPairService ?? new FakeAutoPairService(),
            navigationService ?? new FakeNavigationService());

    [Fact]
    public void RunExplainCommands_BlockedWhileAnyTabExecutionRunning()
    {
        var executionLock = new ExecutionLockService();
        executionLock.TryAcquire(new object());
        var viewModel = CreateViewModel(executionLock: executionLock);
        viewModel.Text = "Load the article from \"a.txt\".";

        Assert.False(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void RunExplainCommands_BlockedWhileTextEmpty()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void RunExplainCommands_ReenableWhenExecutionLockReleases()
    {
        var executionLock = new ExecutionLockService();
        var token = new object();
        executionLock.TryAcquire(token);
        var viewModel = CreateViewModel(executionLock: executionLock);
        viewModel.Text = "Load the article from \"a.txt\".";
        Assert.False(viewModel.RunCommand.CanExecute(null));

        executionLock.Release(token);

        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.True(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void UndoCommand_RaisesUndoRequested()
    {
        var viewModel = CreateViewModel();
        var raised = false;
        viewModel.UndoRequested += () => raised = true;

        viewModel.UndoCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void RedoCommand_RaisesRedoRequested()
    {
        var viewModel = CreateViewModel();
        var raised = false;
        viewModel.RedoRequested += () => raised = true;

        viewModel.RedoCommand.Execute(null);

        Assert.True(raised);
    }

    // --- bdd-ui-interactions.md §2.5-§2.15 (Tree-sitter Editor Decoration) ---
    // §2.5, §2.6, §2.7, §2.9, §2.12 depend on real Tree-sitter/CST behavior
    // and live in ui/tests/Intellisense/ instead (ARM64-gated, currently red
    // - see the implementation plan). The six below are pure ViewModel-wiring
    // scenarios, testable with fakes with no native dependency.

    /// <summary>bdd-ui-interactions.md §2.7-§2.8: RefreshDecorations populates LocalDiagnostics from DiagnosticService.</summary>
    [Fact]
    public void RefreshDecorations_LocalDiagnosticFound_PopulatesLocalDiagnostics()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Unexpected token", 10, 14)],
        };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService);
        viewModel.Text = "Load the article from";

        viewModel.RefreshDecorations();

        Assert.Single(viewModel.LocalDiagnostics);
        Assert.Equal(new LocalDiagnostic("Unexpected token", 10, 14), viewModel.LocalDiagnostics[0]);
    }

    /// <summary>bdd-ui-interactions.md §2.10: ExpandSelection converts SelectionRange to byte offsets, delegates to IStructuralSelectionService, and applies its result back as char offsets.</summary>
    [Fact]
    public void ExpandSelection_StructuralSelectionServiceReturnsLargerRange_UpdatesSelectionRange()
    {
        var structuralSelectionService = new FakeStructuralSelectionService { RangeToReturn = (0, 13) };
        var viewModel = CreateViewModel(structuralSelectionService: structuralSelectionService);
        viewModel.Text = "Summarize it.";
        viewModel.SelectionRange = (10, 12);

        viewModel.ExpandSelection();

        Assert.Equal((0, 13), viewModel.SelectionRange);
    }

    /// <summary>bdd-ui-interactions.md §2.26: Go to Definition sets SelectionRange to the byte span (converted to char offsets) INavigationService.FindDefinition returns.</summary>
    [Fact]
    public void GoToDefinition_NavigationServiceReturnsSpan_SetsSelectionRange()
    {
        var navigationService = new FakeNavigationService { ResultToReturn = (0, 13) };
        var viewModel = CreateViewModel(navigationService: navigationService);
        viewModel.Text = "Summarize it.";
        viewModel.CursorPosition = 10;

        viewModel.GoToDefinition();

        Assert.Equal((0, 13), viewModel.SelectionRange);
    }

    /// <summary>bdd-ui-interactions.md §2.26: no definition found leaves SelectionRange unchanged.</summary>
    [Fact]
    public void GoToDefinition_NavigationServiceReturnsNull_LeavesSelectionRangeUnchanged()
    {
        var viewModel = CreateViewModel(navigationService: new FakeNavigationService());
        viewModel.Text = "Summarize it.";
        viewModel.SelectionRange = (2, 4);

        viewModel.GoToDefinition();

        Assert.Equal((2, 4), viewModel.SelectionRange);
    }

    /// <summary>bdd-ui-interactions.md §2.11: hover populates HoverInfo on request and clears to null on ClearHover.</summary>
    [Fact]
    public void RequestHoverAt_HoverServiceReturnsContent_PopulatesHoverInfo()
    {
        var hoverService = new FakeHoverService
        {
            HoverToReturn = new HoverInfo { Text = "pronoun", Position = 5 },
        };
        var viewModel = CreateViewModel(hoverService: hoverService);
        viewModel.Text = "Summarize it.";

        viewModel.RequestHoverAt(5);
        Assert.NotNull(viewModel.HoverInfo);
        Assert.Equal("pronoun", viewModel.HoverInfo!.Text);

        viewModel.ClearHover();
        Assert.Null(viewModel.HoverInfo);
    }

    /// <summary>bdd-ui-interactions.md §2.13: completions stay empty when the (fake, standing in for a real free-text-position result) completion service returns none.</summary>
    [Fact]
    public void RequestCompletionsAt_CompletionServiceReturnsEmpty_LeavesCompletionItemsEmpty()
    {
        var viewModel = CreateViewModel(completionService: new FakeCompletionService { ItemsToReturn = [] });
        viewModel.Text = "Load the article from \"article.txt\".";

        viewModel.RequestCompletionsAt(10);

        Assert.Empty(viewModel.CompletionItems);
    }

    /// <summary>bdd-ui-interactions.md §2.14: completions/hover/folding/diagnostics are never gated by IExecutionLockService - only Run/Explain are.</summary>
    [Fact]
    public void EditorDecorationTriggers_ExecutionLockHeldByAnotherTab_StillUpdateNormally()
    {
        var executionLock = new ExecutionLockService();
        executionLock.TryAcquire(new object());

        var completionService = new FakeCompletionService { ItemsToReturn = [new CompletionItem { Text = "from" }] };
        var hoverService = new FakeHoverService { HoverToReturn = new HoverInfo { Text = "keyword", Position = 0 } };
        var foldingService = new FakeFoldingService { FoldsToReturn = [new FoldRegion(0, 10)] };
        var diagnosticService = new FakeDiagnosticService { DiagnosticsToReturn = [new LocalDiagnostic("advisory", 0, 1)] };
        var viewModel = CreateViewModel(
            executionLock: executionLock,
            completionService: completionService,
            diagnosticService: diagnosticService,
            hoverService: hoverService,
            foldingService: foldingService);
        viewModel.Text = "Load the article from \"article.txt\".";

        Assert.True(executionLock.IsAnyExecutionRunning);

        viewModel.RequestCompletionsAt(5);
        viewModel.RequestHoverAt(0);
        viewModel.RefreshDecorations();

        Assert.Single(viewModel.CompletionItems);
        Assert.NotNull(viewModel.HoverInfo);
        Assert.Single(viewModel.FoldRegions);
        Assert.Single(viewModel.LocalDiagnostics);
    }

    /// <summary>bdd-ui-interactions.md §2.15: identical text reparsed twice yields byte-for-byte identical FoldRegions/LocalDiagnostics (the fakes are deterministic stand-ins for a real deterministic parse).</summary>
    [Fact]
    public void RefreshDecorations_CalledTwiceWithIdenticalText_ProducesIdenticalResultsBothTimes()
    {
        var foldingService = new FakeFoldingService { FoldsToReturn = [new FoldRegion(0, 10), new FoldRegion(11, 25)] };
        var diagnosticService = new FakeDiagnosticService { DiagnosticsToReturn = [new LocalDiagnostic("advisory", 3, 5)] };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService, foldingService: foldingService);
        viewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";

        viewModel.RefreshDecorations();
        var firstFolds = viewModel.FoldRegions.ToArray();
        var firstDiagnostics = viewModel.LocalDiagnostics.ToArray();

        viewModel.RefreshDecorations();
        var secondFolds = viewModel.FoldRegions.ToArray();
        var secondDiagnostics = viewModel.LocalDiagnostics.ToArray();

        Assert.Equal(firstFolds, secondFolds);
        Assert.Equal(firstDiagnostics, secondDiagnostics);
    }

    // --- bdd-ui-interactions.md §2.7a, §2.16-§2.19 (squiggles, diagnostic hover, ghost text, Tab-to-accept) ---

    /// <summary>bdd-ui-interactions.md §2.7a: RefreshDecorations now runs synchronously from OnTextChanged, not explicit-call-only.</summary>
    [Fact]
    public void OnTextChanged_SettingText_AutomaticallyPopulatesLocalDiagnosticsWithoutExplicitRefreshDecorationsCall()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Unexpected token", 10, 14)],
        };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService);

        viewModel.Text = "Load the article from";

        Assert.Single(viewModel.LocalDiagnostics);
    }

    /// <summary>bdd-ui-interactions.md §2.18: a LocalDiagnostic carrying a SuggestedFix produces a matching QuickFixes entry; one without does not.</summary>
    [Fact]
    public void RefreshDecorations_DiagnosticHasSuggestedFix_PopulatesQuickFixes()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn =
            [
                new LocalDiagnostic("Missing period at end of sentence.", 12, 12, "."),
                new LocalDiagnostic("Unexpected token.", 20, 24),
            ],
        };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService);

        viewModel.Text = "Load the article";

        var fix = Assert.Single(viewModel.QuickFixes);
        Assert.Equal(12, fix.InsertionByte);
        Assert.Equal(".", fix.InsertText);
    }

    /// <summary>bdd-ui-interactions.md §2.18: GhostSuggestion tracks whichever QuickFixes entry sits at the current CursorPosition, and clears when the caret moves away.</summary>
    [Fact]
    public void OnCursorPositionChanged_AtQuickFixInsertionByte_PopulatesGhostSuggestion()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Missing period at end of sentence.", 12, 12, ".")],
        };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService);
        viewModel.Text = "Load the article";

        viewModel.CursorPosition = 12;
        Assert.NotNull(viewModel.GhostSuggestion);
        Assert.Equal(".", viewModel.GhostSuggestion!.InsertText);

        viewModel.CursorPosition = 0;
        Assert.Null(viewModel.GhostSuggestion);
    }

    /// <summary>bdd-ui-interactions.md §2.19: ApplyQuickFixCommand splices InsertText into Text at InsertionByte, moves the caret past it, and clears GhostSuggestion.</summary>
    [Fact]
    public void ApplyQuickFix_SplicesInsertTextAtInsertionByteAndClearsGhostSuggestion()
    {
        var viewModel = CreateViewModel();
        viewModel.Text = "Load the article";

        viewModel.ApplyQuickFixCommand.Execute(new QuickFixItem { Title = "Insert '.'", InsertionByte = 16, InsertText = "." });

        Assert.Equal("Load the article.", viewModel.Text);
        Assert.Equal(17, viewModel.CursorPosition);
        Assert.Null(viewModel.GhostSuggestion);
    }

    /// <summary>bdd-ui-interactions.md §2.17: hovering a byte inside a LocalDiagnostics span shows that diagnostic's message, taking priority over grammar-role hover for the same position.</summary>
    [Fact]
    public void RequestHoverAt_CursorInsideLocalDiagnosticSpan_ReturnsDiagnosticMessageOverGrammarHover()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Missing period at end of sentence.", 12, 12, ".")],
        };
        var hoverService = new FakeHoverService { HoverToReturn = new HoverInfo { Text = "keyword", Position = 12 } };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService, hoverService: hoverService);
        viewModel.Text = "Load the article";

        viewModel.RequestHoverAt(12);

        Assert.NotNull(viewModel.HoverInfo);
        Assert.Equal("Missing period at end of sentence.", viewModel.HoverInfo!.Text);
    }

    /// <summary>bdd-ui-interactions.md §2.17: hovering outside any LocalDiagnostics span falls back to grammar-role hover.</summary>
    [Fact]
    public void RequestHoverAt_CursorOutsideLocalDiagnosticSpan_FallsBackToHoverService()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Missing period at end of sentence.", 12, 12, ".")],
        };
        var hoverService = new FakeHoverService { HoverToReturn = new HoverInfo { Text = "keyword", Position = 0 } };
        var viewModel = CreateViewModel(diagnosticService: diagnosticService, hoverService: hoverService);
        viewModel.Text = "Load the article";

        viewModel.RequestHoverAt(0);

        Assert.NotNull(viewModel.HoverInfo);
        Assert.Equal("keyword", viewModel.HoverInfo!.Text);
    }
}
