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
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
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
            void OnMessage(string message) => _dispatcherQueue.TryEnqueue(async () => await OnNamedPipeMessage(message));

            Task.Run(async () => await NamedPipeProcessor.ProcessNamedPipeAsync(pipeName, connectTimeout: TimeSpan.FromSeconds(10), OnMessage, CancellationToken.None));
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
                if (!AdditionalActionIPCKeys.TryGetValue(messageParts[1], out PasteFormats pasteFormat))
                {
                    Logger.LogWarning($"Unexpected additional action type {messageParts[1]}");
                }
                else
                {
                    await ShowWindow();
                    await viewModel.ExecutePasteFormatAsync(pasteFormat, PasteActionSource.GlobalKeyboardShortcut);
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
