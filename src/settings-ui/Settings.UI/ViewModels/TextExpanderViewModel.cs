// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.TextExpander.Storage;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class TextExpanderViewModel : Observable
{
    private readonly TextExpanderSettingsUtils _settingsUtils;
    private readonly ISettingsRepository<GeneralSettings> _repository;
    private readonly Func<string, int> _sendConfig;
    private readonly Action _launchEditor;
    private readonly GpoRuleConfigured _policy;
    private readonly bool _isElevated;
    private TextExpanderSettings _settings;
    private bool _isEnabled;
    private bool _hasEditorLaunchError;
    private bool _hasSettingsSaveError;

    public TextExpanderViewModel(
        TextExpanderSettingsUtils settingsUtils,
        ISettingsRepository<GeneralSettings> repository,
        Func<string, int> sendConfig,
        bool isElevated,
        Action launchEditor = null,
        Func<GpoRuleConfigured> getPolicy = null)
    {
        _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sendConfig = sendConfig ?? throw new ArgumentNullException(nameof(sendConfig));
        _isElevated = isElevated;
        _launchEditor = launchEditor ?? LaunchEditorProcess;
        _policy = (getPolicy ?? GPOWrapper.GetConfiguredTextExpanderEnabledValue)();
        RefreshEnabledState();
    }

    public bool IsEnabledGpoConfigured => _policy is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;

    public bool CanLaunchEditor => !_isElevated && _policy != GpoRuleConfigured.Disabled;

    public bool IsElevated => _isElevated;

    public bool HasEditorLaunchError
    {
        get => _hasEditorLaunchError;
        private set
        {
            _hasEditorLaunchError = value;
            OnPropertyChanged();
        }
    }

    public bool HasSettingsSaveError
    {
        get => _hasSettingsSaveError;
        private set
        {
            _hasSettingsSaveError = value;
            OnPropertyChanged();
        }
    }

    public bool HasSettingsReadError { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (IsEnabledGpoConfigured || _isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            _repository.SettingsConfig.Enabled.TextExpander = value;
            _sendConfig(new OutGoingGeneralSettings(_repository.SettingsConfig).ToString());
            OnPropertyChanged();
        }
    }

    public int InjectionBackendIndex
    {
        get => _settings.Properties.InjectionBackend.Value switch { "clipboard" => 1, "type" => 2, _ => 0 };
        set
        {
            string backend = value switch { 1 => "clipboard", 2 => "type", _ => "auto" };
            UpdateOptions(properties => properties.InjectionBackend.Value = backend);
            OnPropertyChanged(nameof(IsClipboardThresholdEnabled));
        }
    }

    public bool IsClipboardThresholdEnabled => InjectionBackendIndex == 0;

    public int ClipboardThresholdChars
    {
        get => _settings.Properties.ClipboardThresholdChars.Value;
        set => UpdateOptions(properties => properties.ClipboardThresholdChars.Value = Math.Max(1, value));
    }

    public bool CaseSensitive
    {
        get => _settings.Properties.CaseSensitive;
        set => UpdateOptions(properties => properties.CaseSensitive = value);
    }

    public bool RequireWordBoundary
    {
        get => _settings.Properties.RequireWordBoundary;
        set => UpdateOptions(properties => properties.RequireWordBoundary = value);
    }

    public bool TreatRemapsAsTyping
    {
        get => _settings.Properties.TreatRemapsAsTyping;
        set => UpdateOptions(properties => properties.TreatRemapsAsTyping = value);
    }

    public void LaunchEditor()
    {
        if (!CanLaunchEditor)
        {
            return;
        }

        HasEditorLaunchError = false;
        try
        {
            _launchEditor();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            Logger.LogError("Could not launch the Text Expander editor.", ex);
            HasEditorLaunchError = true;
        }
    }

    public void RefreshEnabledState()
    {
        _isEnabled = IsEnabledGpoConfigured ? _policy == GpoRuleConfigured.Enabled : _repository.SettingsConfig.Enabled.TextExpander;
        try
        {
            // Read-only: replacing an unreadable file with defaults would reset every option and the snippet folder.
            _settings = _settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
            HasSettingsReadError = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Logger.LogError("Could not read Text Expander options.", ex);
            _settings = new TextExpanderSettings();
            HasSettingsReadError = true;
        }

        OnPropertyChanged(string.Empty);
    }

    private static void LaunchEditorProcess()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "PowerToys.TextExpander.Editor.exe");
        var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Text Expander editor process could not be started.");
    }

    private void UpdateOptions(Action<TextExpanderProperties> update, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
    {
        HasSettingsSaveError = false;
        try
        {
            _settings = _settingsUtils.UpdateOptions(update);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Logger.LogError("Could not save Text Expander options.", ex);
            HasSettingsSaveError = true;
        }

        OnPropertyChanged(propertyName);
    }
}
