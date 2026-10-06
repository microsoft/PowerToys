// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
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
    private Process? _runnerProcess;
    private string? _registeredActivationShortcut;
    private string? _registeredControllerShortcut;
    private bool? _controllerEnabled;

    public App()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.ProcessExit += App_Exiting;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (Program.RunnerProcessId is int runnerProcessId)
        {
            try
            {
                _runnerProcess = Process.GetProcessById(runnerProcessId);
            }
            catch (ArgumentException)
            {
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
    }

    private void SettingsTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        LoadSettings();
        try
        {
            if (_runnerProcess?.HasExited == true)
            {
                Exit();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Trace.TraceError($"Could not check PowerToys runner lifetime: {ex}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            var activationShortcut = DefaultActivationShortcut;
            var controllerShortcut = DefaultControllerShortcut;
            var controllerEnabled = true;
            if (File.Exists(_settingsPath))
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
                    Trace.TraceError($"Monitor Power rejected activation shortcut '{activationShortcut}'.");
                    _selectorWindow?.SetStatus($"Unsupported activation shortcut: {activationShortcut}");
                }
            }

            if (_controllerEnabled != controllerEnabled ||
                !string.Equals(controllerShortcut, _registeredControllerShortcut, StringComparison.Ordinal))
            {
                DisplayHelpers.DisableXboxGuideViewCombo();
                _registeredControllerShortcut = controllerShortcut;
                if (controllerEnabled && !DisplayHelpers.EnableControllerChord(controllerShortcut))
                {
                    Trace.TraceError($"Monitor Power rejected controller chord '{controllerShortcut}'.");
                    _selectorWindow?.SetStatus($"Unsupported controller chord: {controllerShortcut}");
                }

                _controllerEnabled = controllerEnabled;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Trace.TraceError($"Could not read Monitor Power settings: {ex}");
            _selectorWindow?.SetStatus($"Could not read Monitor Power settings: {ex.Message}");
        }
    }

    private void ActivationShortcutPressed()
    {
        _ = _dispatcherQueue?.TryEnqueue(ShowSelector);
    }

    private void ControllerShortcutPressed()
    {
        _ = _dispatcherQueue?.TryEnqueue(ShowSelector);
    }

    private void ControllerButtonsPressed(ushort buttons)
    {
        _ = _dispatcherQueue?.TryEnqueue(() => _selectorWindow?.HandleControllerButtons(buttons));
    }

    private void ControllerInputUnavailable(string error)
    {
        Trace.TraceError($"Monitor Power controller input is unavailable: {error}");
        _ = _dispatcherQueue?.TryEnqueue(() => _selectorWindow?.SetStatus("Controller input is unsupported on this system (XInput is unavailable)."));
    }

    private void ShowSelector()
    {
        if (_selectorWindow != null)
        {
            _selectorWindow.Activate();
            return;
        }

        _selectorWindow = new MainWindow();
        _selectorWindow.Closed += (_, _) => _selectorWindow = null;
        _selectorWindow.Activate();
    }

    private void App_Exiting(object? sender, EventArgs e)
    {
        _settingsTimer?.Stop();
        DisplayHelpers.ActivationShortcutPressed -= ActivationShortcutPressed;
        DisplayHelpers.GuideViewComboPressed -= ControllerShortcutPressed;
        DisplayHelpers.ControllerButtonsPressed -= ControllerButtonsPressed;
        DisplayHelpers.ControllerInputUnavailable -= ControllerInputUnavailable;
        DisplayHelpers.UnregisterActivationShortcut();
        DisplayHelpers.DisableXboxGuideViewCombo();
        _runnerProcess?.Dispose();
    }
}
