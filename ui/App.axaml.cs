using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using LimelightX.UI.Views;
using Microsoft.Extensions.Logging;

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
            MainWindow? mainWindowRef = null;
            var filePicker = new FilePickerService(() => mainWindowRef);
            var modal = new ModalService(() => mainWindowRef);
            var configService = new ConfigService();
            var credentialService = new CredentialService();
            var llxProcessService = new LlxProcessService();

            var loadedConfig = configService.Load();
            var initialConfig = loadedConfig ?? new AppConfig();
            var pipelineService = new PipelineService(initialConfig.Port);
            var eventStream = new EventStreamService();
            var executionLock = new ExecutionLockService();

            // App-wide IntelliSense singletons (cnl-editor-architecture.md §5):
            // stateless functions of a caller-supplied CST, so - unlike
            // IParserHost, which is per-tab and constructed inside
            // CnlTabViewModel itself - these are shared across every open tab,
            // exactly like pipelineService/eventStream/executionLock above.
            // queryRunner is the one app-wide compiled-query cache
            // (ui/intellisense/QueryRunner.cs) - every service below that
            // needs a .scm query shares this instance rather than each
            // loading/compiling its own copy.
            var queryRunner = new Lazy<IQueryRunner>(() => new QueryRunner());
            var completionService = new Lazy<ICompletionService>(() => new CompletionService());
            var diagnosticService = new Lazy<IDiagnosticService>(() => new DiagnosticService());
            var hoverService = new Lazy<IHoverService>(() => new HoverService());
            var foldingService = new Lazy<IFoldingService>(() => new FoldingService(queryRunner.Value));
            var structuralSelectionService = new Lazy<IStructuralSelectionService>(() => new StructuralSelectionService());
            var outlineService = new Lazy<IOutlineService>(() => new OutlineService());
            var autoPairService = new Lazy<IAutoPairService>(() => new AutoPairService());
            var navigationService = new Lazy<INavigationService>(() => new NavigationService());

            var tabFactory = new TabFactory(pipelineService, eventStream, executionLock, completionService, diagnosticService, hoverService, foldingService, structuralSelectionService, outlineService, autoPairService, navigationService);
            var workspace = new WorkspaceViewModel(tabFactory, filePicker, modal, executionLock);
            var settings = new SettingsViewModel(configService, credentialService, llxProcessService);
            var about = new AboutViewModel();

            // Persistent diagnostic log (ui-deployment.md §4.3, ui-error-handling.md
            // §2.5) - Microsoft.Extensions.Logging backed by Serilog's file sink.
            // `logger` is a reassignable local: SubscribeLogging closes over
            // `() => logger` so a later rebuild (Settings LogPath change, below)
            // is picked up by every subscription without re-subscribing.
            var loggerFactory = AppLogging.CreateLoggerFactory(initialConfig.LogPath, configService.ConfigFilePath);
            var logger = loggerFactory.CreateLogger("LimelightX");
            SubscribeLogging(workspace.Errors, () => logger);
            SubscribeLogging(settings.Errors, () => logger);
            SubscribeLogging(settings.ErrorBanner.Errors, () => logger);

            // Tabs (and their EditorViewModel/PipelineExecutionViewModel) are
            // created dynamically, not once at startup - hook logging as each
            // new .llx tab opens (ui-viewmodels.md §5.2). Every tab (Cnl or
            // PlainText) has its own ErrorBanner for Open/Save failures
            // (ui-viewmodels.md §13).
            workspace.TabOpened += tab =>
            {
                SubscribeLogging(tab.ErrorBanner.Errors, () => logger);

                if (tab is CnlTabViewModel cnl)
                {
                    SubscribeLogging(cnl.PipelineExecution.Errors, () => logger);
                }
            };

            settings.ConfirmDiscardChangesAsync = () => modal.ShowUnsavedChangesConfirmationAsync();
            settings.CloseRequested = () => workspace.CloseSettingsCommand.Execute(null);
            about.CloseRequested = () => workspace.CloseAboutCommand.Execute(null);
            settings.RelaunchSucceeded += port =>
            {
                pipelineService.SetPort(port);
                _ = eventStream.ConnectAsync(port);

                // ui-viewmodels.md §9: a successful Save redirects logging to the
                // new LogPath immediately - dispose the old factory (closing its
                // Serilog file handle) before building the new one.
                loggerFactory.Dispose();
                loggerFactory = AppLogging.CreateLoggerFactory(settings.LogPath, configService.ConfigFilePath);
                logger = loggerFactory.CreateLogger("LimelightX");
            };

            // Folder/recent-folder persistence (ui-routing-navigation.md §4.1).
            // WorkspaceViewModel itself has no IConfigService dependency (it
            // must not depend on services beyond ITabFactory/IFilePickerService/
            // IModalService/IExecutionLockService) - the composition root
            // persists on its behalf via FolderOpened.
            workspace.FolderOpened += path =>
            {
                var recentFolders = new List<string> { path };
                recentFolders.AddRange(initialConfig.RecentFolders.Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase)));
                configService.Save(initialConfig with { LastOpenedFolder = path, RecentFolders = recentFolders.Take(5).ToList() });
            };

            var mainWindow = new MainWindow(workspace, settings, about);
            mainWindowRef = mainWindow;
            desktop.MainWindow = mainWindow;

            // Restoring the last-opened folder needs no backend and is never
            // gated by first-run/broken-config status (ui-routing-navigation.md
            // §9) - it happens independently of RunStartupSequenceAsync below.
            if (!string.IsNullOrEmpty(initialConfig.LastOpenedFolder) && Directory.Exists(initialConfig.LastOpenedFolder))
            {
                workspace.OpenRoot(initialConfig.LastOpenedFolder);
            }

            _ = RunStartupSequenceAsync(workspace, credentialService, llxProcessService, eventStream, initialConfig, modal, configFileExisted: loadedConfig is not null);

            desktop.ShutdownRequested += (_, _) =>
            {
                pipelineService.Dispose();
                eventStream.Dispose();
                if (queryRunner.IsValueCreated)
                {
                    queryRunner.Value.Dispose();
                }
                loggerFactory.Dispose();
                _ = llxProcessService.StopAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// App startup sequence: load config -> read API key -> if either
    /// missing/invalid, auto-open the Settings modal and leave Run/Explain
    /// disabled on every .llx tab until it's saved (ui-routing-navigation.md
    /// §9) - the Explorer and Tab Strip need no backend and are never gated.
    /// </summary>
    private static async Task RunStartupSequenceAsync(
        WorkspaceViewModel workspace,
        ICredentialService credentialService,
        ILlxProcessService llxProcessService,
        IEventStreamService eventStream,
        AppConfig config,
        IModalService modal,
        bool configFileExisted)
    {
        var apiKey = credentialService.ReadApiKey();

        if (!configFileExisted || string.IsNullOrEmpty(apiKey))
        {
            workspace.IsSettingsOpen = true;
            return;
        }

        var outcome = await llxProcessService.StartAsync(config.Port, apiKey);
        if (!outcome.Success)
        {
            workspace.IsSettingsOpen = true;
            await modal.ShowFatalErrorAsync(outcome.ErrorMessage ?? "Failed to start llx serve.");
            return;
        }

        // Connect before Run/Explain become reachable, so no event can ever
        // be dropped for lack of a connected client (api.md §2.3).
        await eventStream.ConnectAsync(config.Port);
    }

    private static void SubscribeLogging<T>(ObservableCollection<T> collection, Func<ILogger> getLogger)
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
                AppLogging.LogUiError(getLogger(), item);
            }
        };
    }
}
