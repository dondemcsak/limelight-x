using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;
using Xunit;
using VmResultContentType = LimelightX.UI.ViewModels.Inspectors.ResultContentType;

namespace LimelightX.UI.Tests.Inspector;

/// <summary>ui-testing.md §9: inspector expand/collapse and reset behavior.</summary>
public class InspectorViewModelTests
{
    private static AstNode MakeNode() => new()
    {
        Type = "Program",
        Value = string.Empty,
        Span = new Span(),
        Metadata = new AstNodeMetadata(),
        Children = [],
    };

    [Fact]
    public void RawAstViewModel_DefaultsToExpanded()
    {
        var viewModel = new RawAstViewModel();
        Assert.False(viewModel.IsCollapsed);
    }

    [Fact]
    public void RawAstViewModel_ToggleCollapse_ChangesState()
    {
        var viewModel = new RawAstViewModel { IsCollapsed = false };

        viewModel.IsCollapsed = true;
        Assert.True(viewModel.IsCollapsed);

        viewModel.IsCollapsed = false;
        Assert.False(viewModel.IsCollapsed);
    }

    [Fact]
    public void RawAstViewModel_Reset_ClearsTreeAndErrors()
    {
        var viewModel = new RawAstViewModel
        {
            Tree = MakeNode(),
            RawText = "some text",
            Metadata = new AstMetadata { NodeCount = 3 },
        };
        viewModel.Errors.Add(new LimelightX.UI.ViewModels.Errors.UiError
        {
            Code = "ERR_TEST",
            Message = "test",
            Severity = LimelightX.UI.ViewModels.Errors.ErrorSeverity.Error,
            Category = LimelightX.UI.ViewModels.Errors.ErrorCategory.Pipeline,
        });

        viewModel.Reset();

        Assert.Null(viewModel.Tree);
        Assert.Equal(string.Empty, viewModel.RawText);
        Assert.Null(viewModel.Metadata);
        Assert.Empty(viewModel.Errors);
    }

    [Fact]
    public void IrViewModel_Reset_ClearsOperations()
    {
        var viewModel = new IrViewModel();
        viewModel.Operations.Add(new IrOperation
        {
            Type = "Load",
            SourceSpan = new Span(),
            NormalizedSource = "Load...",
            DebugInfo = new DebugInfo(),
        });

        viewModel.Reset();

        Assert.Empty(viewModel.Operations);
    }

    [Fact]
    public void PromptViewModel_Reset_ClearsPrompts()
    {
        var viewModel = new PromptViewModel();
        viewModel.Prompts.Add(new PromptBlock
        {
            PromptText = "Summarize this",
            Metadata = new PromptBlockMetadata(),
        });

        viewModel.Reset();

        Assert.Empty(viewModel.Prompts);
    }

    [Fact]
    public void PromptViewModel_HasPrompts_TracksCollectionAndResetsOnReset()
    {
        var viewModel = new PromptViewModel();
        Assert.False(viewModel.HasPrompts);

        viewModel.Prompts.Add(new PromptBlock
        {
            PromptText = "Summarize this",
            Metadata = new PromptBlockMetadata(),
        });
        Assert.True(viewModel.HasPrompts);

        viewModel.Reset();
        Assert.False(viewModel.HasPrompts);
    }

    [Fact]
    public void ModelOutputViewModel_Reset_ClearsOutputs()
    {
        var viewModel = new ModelOutputViewModel();
        viewModel.Outputs.Add(new ModelOutputBlock
        {
            RawText = "output",
            ContentType = LimelightX.UI.Services.Dto.ResultContentType.Plain,
            Parsed = new ParsedContent(),
            Metadata = new ModelOutputMetadata(),
        });

        viewModel.Reset();

        Assert.Empty(viewModel.Outputs);
    }

    [Fact]
    public void ModelOutputViewModel_HasOutputs_TracksCollectionAndResetsOnReset()
    {
        var viewModel = new ModelOutputViewModel();
        Assert.False(viewModel.HasOutputs);

        viewModel.Outputs.Add(new ModelOutputBlock
        {
            RawText = "output",
            ContentType = LimelightX.UI.Services.Dto.ResultContentType.Plain,
            Parsed = new ParsedContent(),
            Metadata = new ModelOutputMetadata(),
        });
        Assert.True(viewModel.HasOutputs);

        viewModel.Reset();
        Assert.False(viewModel.HasOutputs);
    }

    [Fact]
    public void FinalResultViewModel_Reset_ClearsResultAndContentType()
    {
        var viewModel = new FinalResultViewModel
        {
            ResultText = "the answer",
            ContentType = VmResultContentType.Markdown,
        };

        viewModel.Reset();

        Assert.Equal(string.Empty, viewModel.ResultText);
        Assert.Equal(VmResultContentType.PlainText, viewModel.ContentType);
    }
}
