// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace ColorPicker
{
    public partial class App : Application, IDisposable
    {
        private readonly string[] _args;
        private Mutex _instanceMutex;
        private int _powerToysRunnerPid;
        private bool _disposed;

        private static IServiceProvider _serviceProvider;

        public static Window Window { get; private set; }

        // ETW diagnostic trace session (matches the WPF original and the WinUI 3 sibling modules,
        // e.g. EnvironmentVariables/AdvancedPaste). Constructed once; the session is torn down on
        // process exit, so no explicit Dispose is required (siblings follow the same pattern).
        public ETWTrace EtwTrace { get; } = new ETWTrace();

        public App(string[] args)
        {
            _args = args ?? Array.Empty<string>();

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Language initialization error: " + ex.Message);
            }

            _serviceProvider = ConfigureServices();

            // Environment.Exit on the runner-exit / terminate / single-instance paths raises no
            // Window.Closed, so the cursor-restore in MouseInfoProvider.DisposeHook never runs.
            // Restore the system cursors on any process exit (idempotent) so an abnormal exit while
            // a pick is active with ChangeCursor enabled does not leave the crosshair as the
            // system-wide cursor — parity with the WPF App.OnExit.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            InitializeComponent();
            UnhandledException += App_UnhandledException;
        }

        public bool IsRunningDetachedFromPowerToys() => _powerToysRunnerPid == -1;

        public static T GetService<T>()
            where T : class
            => _serviceProvider.GetService(typeof(T)) as T;

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Allow only one instance of Color Picker.
            // Single-instance guard. The mutex is intentionally released by OS process
            // exit (this is an unpackaged single-process app); no explicit ReleaseMutex.
            _instanceMutex = new Mutex(true, @"Local\PowerToys_ColorPicker_InstanceMutex", out bool createdNew);
            if (!createdNew)
            {
                Logger.LogWarning("There is a ColorPicker instance running. Exiting Color Picker");
                _instanceMutex = null;
                Environment.Exit(0);
                return;
            }

            if (_args.Length > 0 && int.TryParse(_args[0], out _powerToysRunnerPid))
            {
                Logger.LogInfo($"Color Picker started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");
                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting ColorPicker");
                    GetService<CancellationTokenSource>()?.Cancel();
                    Environment.Exit(0);
                });
            }
            else
            {
                _powerToysRunnerPid = -1;
            }

            // Build the picking overlay first so the DI graph (AppStateHandler etc.) can read
            // App.Window, then resolve the main view model (which wires the named events + input)
            // and host it in the overlay's ColorPickerView. The overlay starts hidden (TransparentWindow)
            // and is shown on the hotkey / ShowColorPicker event.
            var overlay = new ColorPickerOverlayWindow();
            Window = overlay;

            var hwnd = overlay.GetWindowHandle();
            var mainViewModel = GetService<ColorPicker.ViewModelContracts.IMainViewModel>();
            overlay.ColorPickerViewControl.DataContext = mainViewModel;
            mainViewModel.RegisterWindowHandle(hwnd);

            // Make the overlay size to the tooltip and follow the cursor while shown (the
            // IMouseInfoProvider singleton is the same one the MainViewModel reads).
            overlay.InitializeCursorFollow(GetService<ColorPicker.Mouse.IMouseInfoProvider>());
        }

        private static IServiceProvider ConfigureServices()
            => ColorPicker.Foundation.AppServices.Configure();

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            // Environment.Exit and normal shutdown both raise ProcessExit; run the deterministic
            // cleanup here since the WinUI Application model never calls Dispose() on the App.
            Mouse.CursorManager.RestoreOriginalCursors();
            Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _instanceMutex?.Dispose();
                _instanceMutex = null;
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
