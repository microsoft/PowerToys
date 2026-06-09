// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.Core;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace Awake
{
    /// <summary>
    /// WinUI application host for Awake. Owns the (initially hidden) flyout window
    /// and the tray icon service that toggles its visibility.
    /// </summary>
    public partial class AwakeApp : Application, IDisposable
    {
        private readonly bool _startedFromPowerToys;

        private MainWindow? _mainWindow;
        private TrayIconService? _trayIconService;
        private bool _disposed;

        public static new AwakeApp? Current { get; private set; }

        public MainWindow? MainWindow => _mainWindow;

        public AwakeApp(bool startedFromPowerToys)
        {
            _startedFromPowerToys = startedFromPowerToys;
            Current = this;

            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                Logger.LogInfo("AwakeApp.OnLaunched: creating MainWindow");
                _mainWindow = new MainWindow(_startedFromPowerToys);

                Logger.LogInfo("AwakeApp.OnLaunched: creating TrayIconService");
                _trayIconService = new TrayIconService(toggleWindow: () => _mainWindow?.ToggleWindow());

                _trayIconService.SetupTrayIcon(Constants.FullAppName, TrayIconService.DefaultIcon);

                // Apply the current Awake mode (this also updates the tray icon to match).
                Manager.SetModeShellIcon(forceAdd: true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"AwakeApp.OnLaunched failed: {ex}");
            }
        }

        public void UpdateTrayIcon(System.Drawing.Icon icon, string tooltip)
        {
            _trayIconService?.UpdateIcon(icon, tooltip);
        }

        public void Shutdown()
        {
            Logger.LogInfo("AwakeApp.Shutdown");
            _trayIconService?.Destroy();
            _trayIconService = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mainWindow?.Dispose();
            _mainWindow = null;
            _trayIconService?.Destroy();
            _trayIconService = null;
            GC.SuppressFinalize(this);
        }

        private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError($"AwakeApp unhandled exception: {e.Exception}");
        }
    }
}
