// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Xaml;

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

            // DI container is configured in Task 3; for now an empty provider.
            _serviceProvider = ConfigureServices();

            InitializeComponent();
            UnhandledException += App_UnhandledException;
        }

        /// <summary>Gets the runner PID, or -1 when running detached from PowerToys.</summary>
        public int RunnerPid => _powerToysRunnerPid;

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
                    GetService<System.Threading.CancellationTokenSource>()?.Cancel();
                    Environment.Exit(0);
                });
            }
            else
            {
                _powerToysRunnerPid = -1;
            }

            Window = new MainWindow();
            Window.Activate();
        }

        /// <summary>
        /// Gets the application-wide cancellation token (replaces the WPF
        /// <c>[Export] ExitToken</c>). Valid only after the <see cref="App"/> has been
        /// constructed (the DI provider is built in the constructor); do not read it
        /// from a static initializer.
        /// </summary>
        public static System.Threading.CancellationToken ExitToken =>
            GetService<System.Threading.CancellationTokenSource>().Token;

        private static IServiceProvider ConfigureServices()
            => ColorPicker.Foundation.AppServices.Configure();

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _instanceMutex?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
