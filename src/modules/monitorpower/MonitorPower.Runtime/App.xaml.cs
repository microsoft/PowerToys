// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MonitorPower;

namespace MonitorPower.Runtime;

public sealed partial class App : Application
{
    private const string DefaultActivationShortcut = "Win + Shift + P";
    private const string DefaultControllerShortcut = "Guide + View";
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MonitorPower",
        "settings.json");

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _settingsTimer;
    private MainWindow? _selectorWindow;
    private Process? _ownerProcess;
    private string? _registeredActivationShortcut;
    private string? _registeredControllerShortcut;
    private bool? _controllerEnabled;
    private bool _hasLoadedSettings;
    private bool _loadedSettingsExists;
    private DateTime _loadedSettingsWriteTimeUtc;
    private long _loadedSettingsLength;
    private bool _settingsReadFailed;
    private DateTime _retrySettingsReadAfterUtc;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.ProcessExit += App_Exiting;
        RuntimeLog.Info("Runtime application object created.");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var startupTimer = Stopwatch.StartNew();
        RuntimeLog.Info($"Runtime application launch started. Owner PID: {Program.OwnerProcessId?.ToString(CultureInfo.InvariantCulture) ?? "not provided"}.");
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (Program.OwnerProcessId is int ownerProcessId)
        {
            try
            {
                _ownerProcess = Process.GetProcessById(ownerProcessId);
                RuntimeLog.Info($"Attached to owner process {ownerProcessId}.");
            }
            catch (ArgumentException)
            {
                RuntimeLog.Warning($"Owner process {ownerProcessId} was not found; runtime host will exit.");
                Exit();
                return;
            }
        }

        DisplayHelpers.ActivationShortcutPressed += ActivationShortcutPressed;
        DisplayHelpers.GuideViewComboPressed += ControllerShortcutPressed;
        DisplayHelpers.ControllerButtonsPressed += ControllerButtonsPressed;
        DisplayHelpers.ControllerInputUnavailable += ControllerInputUnavailable;

        LoadSettings();
        _settingsTimer = _dispatcherQueue.CreateTimer();
        _settingsTimer.Interval = TimeSpan.FromSeconds(1);
        _settingsTimer.Tick += SettingsTimer_Tick;
        _settingsTimer.Start();
        RuntimeLog.Info($"Runtime application launch completed in {startupTimer.ElapsedMilliseconds} ms.");
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        RuntimeLog.Error("Unhandled WinUI exception in Monitor Power runtime.", e.Exception);
    }

    private void SettingsTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        LoadSettings();
        try
        {
            if (_ownerProcess?.HasExited == true)
            {
                RuntimeLog.Info("Owner process exited; stopping Monitor Power runtime.");
                Exit();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            RuntimeLog.Error("Could not check owner process lifetime.", ex);
        }
    }

    private void LoadSettings()
    {
        try
        {
            var settingsFile = new FileInfo(_settingsPath);
            var settingsExists = settingsFile.Exists;
            var lastWriteTimeUtc = settingsExists ? settingsFile.LastWriteTimeUtc : DateTime.MinValue;
            var settingsLength = settingsExists ? settingsFile.Length : 0;
            if (_hasLoadedSettings &&
                settingsExists == _loadedSettingsExists &&
                lastWriteTimeUtc == _loadedSettingsWriteTimeUtc &&
                settingsLength == _loadedSettingsLength &&
                (!_settingsReadFailed || DateTime.UtcNow < _retrySettingsReadAfterUtc))
            {
                return;
            }

            _hasLoadedSettings = true;
            _loadedSettingsExists = settingsExists;
            _loadedSettingsWriteTimeUtc = lastWriteTimeUtc;
            _loadedSettingsLength = settingsLength;

            var activationShortcut = DefaultActivationShortcut;
            var controllerShortcut = DefaultControllerShortcut;
            var controllerEnabled = true;
            if (settingsExists)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
                var root = document.RootElement;
                if (root.TryGetProperty("activationShortcut", out var activationValue) &&
                    activationValue.ValueKind == JsonValueKind.String)
                {
                    activationShortcut = activationValue.GetString() ?? DefaultActivationShortcut;
                }

                if (root.TryGetProperty("controllerShortcut", out var controllerValue) &&
                    controllerValue.ValueKind == JsonValueKind.String)
                {
                    controllerShortcut = controllerValue.GetString() ?? DefaultControllerShortcut;
                }

                if (root.TryGetProperty("xboxGuideViewEnabled", out var enabledValue) &&
                    enabledValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    controllerEnabled = enabledValue.GetBoolean();
                }
            }

            if (!string.Equals(activationShortcut, _registeredActivationShortcut, StringComparison.Ordinal))
            {
                _registeredActivationShortcut = activationShortcut;
                if (!DisplayHelpers.RegisterActivationShortcut(activationShortcut))
                {
                    DisplayHelpers.UnregisterActivationShortcut();
                    RuntimeLog.Warning($"Activation shortcut registration rejected: '{activationShortcut}'.");
                    _selectorWindow?.SetStatus($"Unsupported activation shortcut: {activationShortcut}");
                }
                else
                {
                    RuntimeLog.Info($"Activation shortcut registration updated: '{activationShortcut}'.");
                }
            }

            if (_controllerEnabled != controllerEnabled ||
                !string.Equals(controllerShortcut, _registeredControllerShortcut, StringComparison.Ordinal))
            {
                DisplayHelpers.DisableXboxGuideViewCombo();
                _registeredControllerShortcut = controllerShortcut;
                if (controllerEnabled && !DisplayHelpers.EnableControllerChord(controllerShortcut))
                {
                    RuntimeLog.Warning($"Controller chord registration rejected: '{controllerShortcut}'.");
                    _selectorWindow?.SetStatus($"Unsupported controller chord: {controllerShortcut}");
                }
                else
                {
                    RuntimeLog.Info($"Controller listener configuration updated. Enabled={controllerEnabled}, chord='{controllerShortcut}'.");
                }

                _controllerEnabled = controllerEnabled;
            }

            _settingsReadFailed = false;
            RuntimeLog.Info($"Settings loaded in {_settingsPath}. Exists={settingsExists}, bytes={settingsLength}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _settingsReadFailed = true;
            _retrySettingsReadAfterUtc = DateTime.UtcNow.AddSeconds(10);
            RuntimeLog.Error("Could not read Monitor Power settings; retrying in 10 seconds unless the file changes.", ex);
            _selectorWindow?.SetStatus($"Could not read Monitor Power settings: {ex.Message}");
        }
    }

    private void ActivationShortcutPressed()
    {
        RuntimeLog.Info("Activation shortcut pressed.");
        _ = _dispatcherQueue?.TryEnqueue(ShowSelector);
    }

    private void ControllerShortcutPressed()
    {
        RuntimeLog.Info("Controller activation chord pressed.");
        _ = _dispatcherQueue?.TryEnqueue(ShowSelector);
    }

    private void ControllerButtonsPressed(ushort buttons)
    {
        _ = _dispatcherQueue?.TryEnqueue(() => _selectorWindow?.HandleControllerButtons(buttons));
    }

    private void ControllerInputUnavailable(string error)
    {
        RuntimeLog.Warning($"Controller input is unavailable: {error}");
        _ = _dispatcherQueue?.TryEnqueue(() => _selectorWindow?.SetStatus("Controller input is unsupported on this system (XInput is unavailable)."));
    }

    private void ShowSelector()
    {
        if (_selectorWindow != null)
        {
            RuntimeLog.Info("Existing profile selector activated.");
            _selectorWindow.Activate();
            return;
        }

        RuntimeLog.Info("Creating profile selector window.");
        _selectorWindow = new MainWindow();
        _selectorWindow.Closed += (_, _) =>
        {
            RuntimeLog.Info("Profile selector window closed.");
            _selectorWindow = null;
        };
        _selectorWindow.Activate();
    }

    private void App_Exiting(object? sender, EventArgs e)
    {
        RuntimeLog.Info("Runtime host is shutting down; stopping listeners and timer.");
        _settingsTimer?.Stop();
        DisplayHelpers.ActivationShortcutPressed -= ActivationShortcutPressed;
        DisplayHelpers.GuideViewComboPressed -= ControllerShortcutPressed;
        DisplayHelpers.ControllerButtonsPressed -= ControllerButtonsPressed;
        DisplayHelpers.ControllerInputUnavailable -= ControllerInputUnavailable;
        DisplayHelpers.UnregisterActivationShortcut();
        DisplayHelpers.DisableXboxGuideViewCombo();
        _ownerProcess?.Dispose();
    }
}
