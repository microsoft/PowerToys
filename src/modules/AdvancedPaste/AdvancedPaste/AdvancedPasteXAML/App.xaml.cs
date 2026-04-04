// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using AdvancedPaste.ViewModels;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinUIEx;

using static AdvancedPaste.Helpers.NativeMethods;

using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace AdvancedPaste
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        public IHost Host { get; private set; }

        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

        private static readonly Dictionary<string, PasteFormats> AdditionalActionIPCKeys =
                 typeof(PasteFormats).GetFields()
                                     .Where(field => field.IsLiteral)
                                     .Select(field => (Format: (PasteFormats)field.GetRawConstantValue(), field.GetCustomAttribute<PasteFormatMetadataAttribute>().IPCKey))
                                     .Where(field => field.IPCKey != null)
                                     .ToDictionary(field => field.IPCKey, field => field.Format);

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly OptionsViewModel viewModel;

        private MainWindow window;

        private nint windowHwnd;

        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().UseContentRoot(AppContext.BaseDirectory).ConfigureServices((context, services) =>
            {
                services.AddSingleton<IFileSystem, FileSystem>();
                services.AddSingleton<IUserSettings, UserSettings>();
                services.AddSingleton<IAICredentialsProvider, EnhancedVaultCredentialsProvider>();
                services.AddSingleton<IPromptModerationService, Services.OpenAI.PromptModerationService>();
                services.AddSingleton<IKernelQueryCacheService, CustomActionKernelQueryCacheService>();
                services.AddSingleton<IPasteAIProviderFactory, PasteAIProviderFactory>();
                services.AddSingleton<ICustomActionTransformService, CustomActionTransformService>();
                services.AddSingleton<IKernelService, AdvancedAIKernelService>();
                services.AddSingleton<IPasteFormatExecutor, PasteFormatExecutor>();
                services.AddSingleton<OptionsViewModel>();
            }).Build();

            viewModel = GetService<OptionsViewModel>();

            UnhandledException += App_UnhandledException;

            // Start a background pipe server so Settings UI can query Phi Silica status.
            // This runs with MSIX package identity, so LAF + GetReadyState() work.
            StartPhiSilicaStatusServer();
        }

        public MainWindow GetMainWindow()
        {
            return window;
        }

        public static T GetService<T>()
            where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        /// <summary>
        /// Starts a background named pipe server that responds to Phi Silica status queries
        /// from Settings UI. The pipe runs for the lifetime of the app.
        /// </summary>
        private static void StartPhiSilicaStatusServer()
        {
            Task.Run(async () =>
            {
                // Check status once (with MSIX identity) and cache
                string status;
                try
                {
                    PhiSilicaLafHelper.TryUnlock();
                    var readyState = Microsoft.Windows.AI.Text.LanguageModel.GetReadyState();
                    status = readyState switch
                    {
                        Microsoft.Windows.AI.AIFeatureReadyState.NotSupportedOnCurrentSystem => "NotSupported",
                        Microsoft.Windows.AI.AIFeatureReadyState.NotReady => "NotReady",
                        _ => "Available",
                    };
                }
                catch
                {
                    status = "NotSupported";
                }

                Logger.LogDebug($"Phi Silica status: {status}");

                // Serve status to any client that connects
                while (true)
                {
                    try
                    {
                        using var server = new System.IO.Pipes.NamedPipeServerStream(
                            "powertoys_advancedpaste_phi_status",
                            System.IO.Pipes.PipeDirection.Out,
                            1,
                            System.IO.Pipes.PipeTransmissionMode.Byte,
                            System.IO.Pipes.PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync();

                        var bytes = System.Text.Encoding.UTF8.GetBytes(status);
                        await server.WriteAsync(bytes);
                        server.WaitForPipeDrain();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Phi Silica status pipe error", ex);
                        await Task.Delay(1000);
                    }
                }
            });
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Try protocol activation args first (MSIX launch via x-advancedpaste:// URI)
            try
            {
                var activatedArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                Logger.LogDebug($"Activation kind: {activatedArgs?.Kind}");

                if (activatedArgs?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Protocol &&
                    activatedArgs.Data is Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                {
                    var uri = protocolArgs.Uri;
                    Logger.LogDebug($"Protocol URI: {uri}");

                    // Parse query parameters manually to avoid System.Web dependency
                    var pid = GetQueryParam(uri.Query, "pid");
                    var pipeName = GetQueryParam(uri.Query, "pipe");

                    Logger.LogDebug($"Parsed pid={pid}, pipe={pipeName}");

                    if (int.TryParse(pid, out int powerToysRunnerPid))
                    {
                        RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                        {
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                Dispose();
                                Environment.Exit(0);
                            });
                        });
                    }

                    if (!string.IsNullOrEmpty(pipeName))
                    {
                        ProcessNamedPipe(pipeName);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to process protocol activation args", ex);
            }

            // Fallback: command-line args (direct exe launch)
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[1], out int powerToysRunnerPid))
                {
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            Dispose();
                            Environment.Exit(0);
                        });
                    });
                }
            }

            if (cmdArgs?.Length > 2)
            {
                ProcessNamedPipe(cmdArgs[2]);
            }
        }

        private void ProcessNamedPipe(string pipeName)
        {
            Logger.LogDebug($"Connecting to named pipe: {pipeName}");
            void OnMessage(string message) => _dispatcherQueue.TryEnqueue(async () => await OnNamedPipeMessage(message));

            Task.Run(async () => await NamedPipeProcessor.ProcessNamedPipeAsync(pipeName, connectTimeout: TimeSpan.FromSeconds(10), OnMessage, CancellationToken.None));
        }

        private static string GetQueryParam(string query, string key)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            // Remove leading '?' if present
            var q = query.StartsWith('?') ? query.Substring(1) : query;
            foreach (var part in q.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }

            return null;
        }

        private async Task OnNamedPipeMessage(string message)
        {
            var messageParts = message.Split();
            var messageType = messageParts.First();

            if (messageType == PowerToys.Interop.Constants.AdvancedPasteShowUIMessage())
            {
                await ShowWindow();
            }
            else if (messageType == PowerToys.Interop.Constants.AdvancedPasteMarkdownMessage())
            {
                await viewModel.ExecutePasteFormatAsync(PasteFormats.Markdown, PasteActionSource.GlobalKeyboardShortcut);
            }
            else if (messageType == PowerToys.Interop.Constants.AdvancedPasteJsonMessage())
            {
                await viewModel.ExecutePasteFormatAsync(PasteFormats.Json, PasteActionSource.GlobalKeyboardShortcut);
            }
            else if (messageType == PowerToys.Interop.Constants.AdvancedPasteAdditionalActionMessage())
            {
                await OnAdvancedPasteAdditionalActionHotkey(messageParts);
            }
            else if (messageType == PowerToys.Interop.Constants.AdvancedPasteCustomActionMessage())
            {
                await OnAdvancedPasteCustomActionHotkey(messageParts);
            }
            else if (messageType == PowerToys.Interop.Constants.AdvancedPasteTerminateAppMessage())
            {
                Dispose();
                Environment.Exit(0);
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        private async Task OnAdvancedPasteAdditionalActionHotkey(string[] messageParts)
        {
            if (messageParts.Length != 2)
            {
                Logger.LogWarning("Unexpected additional action message");
            }
            else
            {
                const string coachingSuffix = "-coaching";
                var actionKey = messageParts[1];
                bool forceCoaching = actionKey.EndsWith(coachingSuffix, StringComparison.OrdinalIgnoreCase);

                if (forceCoaching)
                {
                    actionKey = actionKey[..^coachingSuffix.Length];
                }

                if (!AdditionalActionIPCKeys.TryGetValue(actionKey, out PasteFormats pasteFormat))
                {
                    Logger.LogWarning($"Unexpected additional action type {messageParts[1]}");
                }
                else
                {
                    await ShowWindow();
                    await viewModel.ExecutePasteFormatAsync(pasteFormat, PasteActionSource.GlobalKeyboardShortcut, forceCoaching);
                }
            }
        }

        private async Task OnAdvancedPasteCustomActionHotkey(string[] messageParts)
        {
            if (messageParts.Length != 2)
            {
                Logger.LogWarning("Unexpected custom action message");
            }
            else
            {
                if (!int.TryParse(messageParts[1], CultureInfo.InvariantCulture, out int customActionId))
                {
                    Logger.LogWarning($"Unexpected custom action message id {messageParts[1]}");
                }
                else
                {
                    await ShowWindow();
                    await viewModel.ExecuteCustomActionAsync(customActionId, PasteActionSource.GlobalKeyboardShortcut);
                }
            }
        }

        private async Task ShowWindow()
        {
            await viewModel.OnShowAsync();

            if (window is null)
            {
                window = new MainWindow();
                windowHwnd = window.GetWindowHandle();

                MoveWindowToActiveMonitor();

                window.Activate();
            }
            else
            {
                MoveWindowToActiveMonitor();

                Windows.Win32.PInvoke.ShowWindow((Windows.Win32.Foundation.HWND)windowHwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_SHOW);
                WindowHelpers.BringToForeground(windowHwnd);
            }

            window.SetFocus();
        }

        private void MoveWindowToActiveMonitor()
        {
            if (GetCursorPos(out PointInter cursorPosition))
            {
                DisplayArea displayArea = DisplayArea.GetFromPoint(new PointInt32(cursorPosition.X, cursorPosition.Y), DisplayAreaFallback.Nearest);

                var x = displayArea.WorkArea.X + (displayArea.WorkArea.Width / 2) - (window.Width / 2);
                var y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height / 2) - (window.Height / 2);

                window.MoveAndResize(x, y, window.Width, window.Height);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    EtwTrace?.Dispose();
                    window?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
