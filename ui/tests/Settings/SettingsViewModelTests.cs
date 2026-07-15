using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using Xunit;

namespace LimelightX.UI.Tests.Settings;

public class SettingsViewModelTests
{
    private sealed class FakeConfigService : IConfigService
    {
        public AppConfig? ConfigToReturn { get; set; }
        public AppConfig? SavedConfig { get; private set; }

        public string ConfigFilePath => "fake";

        public AppConfig? Load() => ConfigToReturn;

        public void Save(AppConfig config) => SavedConfig = config;
    }

    private sealed class FakeCredentialService : ICredentialService
    {
        public string? StoredKey { get; set; }

        public string? ReadApiKey() => StoredKey;

        public void WriteApiKey(string apiKey) => StoredKey = apiKey;

        public void DeleteApiKey() => StoredKey = null;
    }

    private sealed class FakeLlxProcessService : ILlxProcessService
    {
        public ProcessStartOutcome OutcomeToReturn { get; set; } = new(true, null);
        public bool IsRunning { get; private set; }
        public int StartCallCount { get; private set; }

        public Task<ProcessStartOutcome> StartAsync(int port, string apiKey)
        {
            StartCallCount++;
            IsRunning = OutcomeToReturn.Success;
            return Task.FromResult(OutcomeToReturn);
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private static (SettingsViewModel ViewModel, FakeConfigService Config, FakeCredentialService Credential, FakeLlxProcessService Process) CreateViewModel()
    {
        var config = new FakeConfigService { ConfigToReturn = new AppConfig { Port = 4747, LogPath = "", EnvironmentProfile = EnvironmentProfile.Dev } };
        var credential = new FakeCredentialService { StoredKey = "existing-key" };
        var process = new FakeLlxProcessService();
        var viewModel = new SettingsViewModel(config, credential, process);
        return (viewModel, config, credential, process);
    }

    [Fact]
    public void Constructor_LoadsFromDiskAndCredentialManager()
    {
        var (viewModel, _, _, _) = CreateViewModel();

        Assert.Equal(4747, viewModel.Port);
        Assert.Equal("existing-key", viewModel.ApiKey);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void EditingAnyField_SetsIsDirty()
    {
        var (viewModel, _, _, _) = CreateViewModel();

        viewModel.Port = 5000;

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task SaveSettingsAsync_OutOfRangePort_BlocksSaveAndAddsError()
    {
        var (viewModel, config, _, process) = CreateViewModel();
        viewModel.Port = 70000;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Null(config.SavedConfig);
        Assert.Equal(0, process.StartCallCount);
        Assert.Contains(viewModel.Errors, e => e.Code == "ERR_INVALID_PORT");
    }

    [Fact]
    public async Task SaveSettingsAsync_EmptyApiKey_BlocksSave()
    {
        var (viewModel, config, _, _) = CreateViewModel();
        viewModel.ApiKey = "";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Null(config.SavedConfig);
        Assert.Contains(viewModel.Errors, e => e.Code == "ERR_MISSING_API_KEY");
    }

    [Fact]
    public async Task SaveSettingsAsync_ValidInput_SavesAndRelaunchesAndClearsDirty()
    {
        var (viewModel, config, credential, process) = CreateViewModel();
        var closed = false;
        viewModel.CloseRequested = () => closed = true;

        viewModel.Port = 5001;
        viewModel.ApiKey = "sk-new-key";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(config.SavedConfig);
        Assert.Equal(5001, config.SavedConfig!.Port);
        Assert.Equal("sk-new-key", credential.StoredKey);
        Assert.Equal(1, process.StartCallCount);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.IsApplying);
        Assert.True(closed);
    }

    [Fact]
    public async Task SaveSettingsAsync_RelaunchFails_ShowsErrorBannerAndKeepsModalOpenAndDirty()
    {
        var (viewModel, _, _, process) = CreateViewModel();
        process.OutcomeToReturn = new ProcessStartOutcome(false, "port already in use");
        var closed = false;
        viewModel.CloseRequested = () => closed = true;

        viewModel.Port = 5002;
        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.ErrorBanner.IsVisible);
        Assert.Contains(viewModel.ErrorBanner.Errors, e => e.Message == "port already in use");
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.IsApplying);
        Assert.False(closed);
    }

    [Fact]
    public void RevertToLastSavedCommand_RestoresOriginalValuesAndClearsDirty()
    {
        var (viewModel, _, _, _) = CreateViewModel();
        viewModel.Port = 9999;
        viewModel.ApiKey = "changed";
        Assert.True(viewModel.IsDirty);

        viewModel.RevertToLastSavedCommand.Execute(null);

        Assert.Equal(4747, viewModel.Port);
        Assert.Equal("existing-key", viewModel.ApiKey);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void ToggleApiKeyVisibilityCommand_TogglesFlag()
    {
        var (viewModel, _, _, _) = CreateViewModel();
        Assert.False(viewModel.IsApiKeyVisible);

        viewModel.ToggleApiKeyVisibilityCommand.Execute(null);
        Assert.True(viewModel.IsApiKeyVisible);

        viewModel.ToggleApiKeyVisibilityCommand.Execute(null);
        Assert.False(viewModel.IsApiKeyVisible);
    }

    [Fact]
    public async Task CancelSettingsCommand_NotDirty_ClosesWithoutConfirmation()
    {
        var (viewModel, _, _, _) = CreateViewModel();
        var closed = false;
        var promptCount = 0;
        viewModel.CloseRequested = () => closed = true;
        viewModel.ConfirmDiscardChangesAsync = () => { promptCount++; return Task.FromResult(true); };

        await viewModel.CancelSettingsCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Equal(0, promptCount);
    }

    [Fact]
    public async Task CancelSettingsCommand_Dirty_PromptsAndStaysOpenIfUserDoesNotDiscard()
    {
        var (viewModel, _, _, _) = CreateViewModel();
        viewModel.Port = 5003;
        var closed = false;
        viewModel.CloseRequested = () => closed = true;
        viewModel.ConfirmDiscardChangesAsync = () => Task.FromResult(false);

        await viewModel.CancelSettingsCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(5003, viewModel.Port);
    }

    [Fact]
    public async Task CancelSettingsCommand_Dirty_RevertsAndClosesWhenUserDiscards()
    {
        var (viewModel, _, _, _) = CreateViewModel();
        viewModel.Port = 5004;
        var closed = false;
        viewModel.CloseRequested = () => closed = true;
        viewModel.ConfirmDiscardChangesAsync = () => Task.FromResult(true);

        await viewModel.CancelSettingsCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(4747, viewModel.Port);
    }
}
