using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LimelightX.UI.Routing;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.Views;

namespace LimelightX.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Composition root (manual wiring - no DI container, per CLAUDE.md §3.5
            // approved-dependency list).
            var navigation = new NavigationViewModel();

            MainWindow? mainWindowRef = null;
            var filePicker = new FilePickerService(() => mainWindowRef);
            var modal = new ModalService(() => mainWindowRef);
            var configService = new ConfigService();
            var credentialService = new CredentialService();
            var llxProcessService = new LlxProcessService();

            var loadedConfig = configService.Load();
            var initialConfig = loadedConfig ?? new AppConfig();
            var pipelineService = new PipelineService(initialConfig.Port);

            var fileLoader = new FileLoaderViewModel(filePicker);
            var editor = new EditorViewModel(pipelineService);
            var pipelineExecution = new PipelineExecutionViewModel(pipelineService);
            var settings = new SettingsViewModel(configService, credentialService, llxProcessService);

            // In-memory-only error log (ui-error-handling.md §11) - no UI surface
            // anywhere in the spec's catalog, purely an internal diagnostic aid.
            var logService = new LogService();
            SubscribeLogging(fileLoader.Errors, logService);
            SubscribeLogging(editor.ValidationErrors, logService);
            SubscribeLogging(pipelineExecution.Errors, logService);
            SubscribeLogging(settings.Errors, logService);

            fileLoader.FileLoaded += content => editor.Text = content;

            // Run/Explain/Trace sequence (ui-routing-navigation.md §5): EditorViewModel
            // already blocked the click via CanExecutePipelineCommand if CNL is
            // invalid (Guard 2); here we call the backend and, per the BDD
            // refinement in bdd-ui-navigation.md, only block navigation (guard
            // modal, stay on Editor) when there's no inspector data at all - a
            // partial pipeline failure still navigates and shows inline/banner errors.
            editor.RunRequested = async source =>
            {
                var outcome = await pipelineExecution.RunPipelineAsync(source);
                await HandleOutcomeAsync(outcome);
            };
            editor.ExplainRequested = async source =>
            {
                var outcome = await pipelineExecution.ExplainPipelineAsync(source);
                await HandleOutcomeAsync(outcome);
            };
            editor.TraceRequested = async source =>
            {
                var outcome = await pipelineExecution.TracePipelineAsync(source);
                await HandleOutcomeAsync(outcome);
            };

            async Task HandleOutcomeAsync(PipelineCallOutcome outcome)
            {
                if (outcome == PipelineCallOutcome.NavigateToExecution)
                {
                    await navigation.NavigateDirectAsync(PageType.Execution);
                    return;
                }

                // Blocked: no inspector data at all. Surface whatever the backend
                // actually said (e.g. a confirmed-empirical ERR_EVALUATOR_FATAL
                // message) rather than a generic string, and use the fatal-styled
                // modal specifically when severity says so (ui-error-handling.md §5).
                if (pipelineExecution.Errors.Count == 0)
                {
                    await modal.ShowBlockedNavigationAsync("The server could not be reached. Check the connection and try again.");
                    return;
                }

                var message = string.Join(" ", pipelineExecution.Errors.Select(e => e.Message));

                if (pipelineExecution.Errors.Any(e => e.Severity == ViewModels.Errors.ErrorSeverity.Fatal))
                {
                    await modal.ShowFatalErrorAsync(message);
                }
                else
                {
                    await modal.ShowBlockedNavigationAsync(message);
                }
            }

            // Guard 1 (ui-routing-navigation.md §4): Home -> Editor requires a loaded file.
            navigation.EditorGuard = () => fileLoader.FileContent is not null
                ? NavigationGuardResult.Allowed()
                : NavigationGuardResult.Blocked("Open a file before continuing to the editor.");

            // Guard 3/9: direct sidebar navigation to Execution requires a pipeline
            // to have produced a result at least once (ui-routing-navigation.md §9) -
            // automatic post-pipeline navigation above bypasses this via NavigateDirectAsync.
            navigation.ExecutionGuard = () => pipelineExecution.HasResult
                ? NavigationGuardResult.Allowed()
                : NavigationGuardResult.Blocked("Run, Explain, or Trace a program before viewing execution results.");

            // Guard 4: no navigation while a pipeline call is in flight.
            navigation.IsExecutionBusy = () =>
                pipelineExecution.IsRunning || pipelineExecution.IsExplaining || pipelineExecution.IsTracing;

            // Guard 5 (ui-routing-navigation.md §4): leaving Settings with unsaved
            // changes shows the Stay/Discard confirmation; Discard reverts fields.
            navigation.SettingsLeaveGuard = async () =>
            {
                if (!settings.IsDirty)
                {
                    return true;
                }

                var discard = await modal.ShowUnsavedChangesConfirmationAsync();
                if (discard)
                {
                    settings.RevertToLastSavedCommand.Execute(null);
                }

                return discard;
            };

            navigation.NavigationBlocked += reason => _ = modal.ShowBlockedNavigationAsync(reason);

            settings.NavigateBackRequested = async () =>
            {
                navigation.IsFirstRunSetupRequired = false;
                await navigation.NavigateBackFromSettingsAsync();
            };
            settings.CancelRequested = async () =>
            {
                var canLeave = await navigation.SettingsLeaveGuard();
                if (canLeave)
                {
                    await navigation.NavigateBackFromSettingsAsync();
                }
            };
            settings.RelaunchFailed += message => _ = modal.ShowFatalErrorAsync(message);

            var mainWindow = new MainWindow(navigation, fileLoader, editor, pipelineExecution, settings);
            mainWindowRef = mainWindow;
            desktop.MainWindow = mainWindow;

            _ = RunStartupSequenceAsync(navigation, credentialService, llxProcessService, initialConfig, modal, configFileExisted: loadedConfig is not null);

            desktop.ShutdownRequested += (_, _) =>
            {
                pipelineService.Dispose();
                _ = llxProcessService.StopAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// App startup sequence (proposed - ui-deployment.md §7 says LimelightX.exe
    /// launches llx.exe serve "when the UI starts" but doesn't sequence it
    /// relative to config-loading/first-run detection): load config -> read
    /// API key -> if either missing/invalid, route to Settings and leave
    /// Home/Editor/Execution unreachable (ui-routing-navigation.md §2) without
    /// starting llx serve; otherwise start it and route to Home, or fall back
    /// to the same Settings-bypass with a fatal modal if that start fails.
    /// </summary>
    private static async Task RunStartupSequenceAsync(
        NavigationViewModel navigation,
        ICredentialService credentialService,
        ILlxProcessService llxProcessService,
        AppConfig config,
        IModalService modal,
        bool configFileExisted)
    {
        var apiKey = credentialService.ReadApiKey();

        if (!configFileExisted || string.IsNullOrEmpty(apiKey))
        {
            navigation.IsFirstRunSetupRequired = true;
            navigation.CurrentPage = PageType.Settings;
            return;
        }

        var outcome = await llxProcessService.StartAsync(config.Port, apiKey);
        if (!outcome.Success)
        {
            navigation.IsFirstRunSetupRequired = true;
            navigation.CurrentPage = PageType.Settings;
            await modal.ShowFatalErrorAsync(outcome.ErrorMessage ?? "Failed to start llx serve.");
        }
    }

    private static void SubscribeLogging<T>(ObservableCollection<T> collection, ILogService logService)
        where T : UiError
    {
        collection.CollectionChanged += (_, e) =>
        {
            if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace) || e.NewItems is null)
            {
                return;
            }

            foreach (T item in e.NewItems)
            {
                logService.Log(item);
            }
        };
    }
}
