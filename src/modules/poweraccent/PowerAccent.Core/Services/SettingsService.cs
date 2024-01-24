// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core.Services;

public class SettingsService
{
    private const string PowerAccentModuleName = "QuickAccent";
    private readonly SettingsUtils _settingsUtils;
    private readonly IFileSystemWatcher _watcher;
    private readonly object _loadingSettingsLock = new object();
    private KeyboardListener _keyboardListener;

    public SettingsService(KeyboardListener keyboardListener)
    {
        _settingsUtils = new SettingsUtils();
        _keyboardListener = keyboardListener;
        ReadSettings();
        _watcher = Helper.GetFileWatcher(PowerAccentModuleName, "settings.json", () => { ReadSettings(); });
    }

    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    private void ReadSettings()
    {
        // TODO this IO call should by Async, update GetFileWatcher helper to support async
        lock (_loadingSettingsLock)
        {
            {
                try
                {
                    if (!_settingsUtils.SettingsExists(PowerAccentModuleName))
                    {
                        Logger.LogInfo("QuickAccent settings.json was missing, creating a new one");
                        var defaultSettings = new PowerAccentSettings();
                        var options = _serializerOptions;

                        _settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), PowerAccentModuleName);
                    }

                    var settings = _settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentModuleName);
                    if (settings != null)
                    {
                        ActivationKey = settings.Properties.ActivationKey;
                        _keyboardListener.UpdateActivationKey((int)ActivationKey);

                        DoNotActivateOnGameMode = settings.Properties.DoNotActivateOnGameMode;
                        _keyboardListener.UpdateDoNotActivateOnGameMode(DoNotActivateOnGameMode);

                        InputTime = settings.Properties.InputTime.Value;
                        _keyboardListener.UpdateInputTime(InputTime);

                        ExcludedApps = settings.Properties.ExcludedApps.Value;
                        _keyboardListener.UpdateExcludedApps(ExcludedApps);

                        SelectedLang = Enum.TryParse(settings.Properties.SelectedLang.Value, out Language selectedLangValue) ? selectedLangValue : Language.ALL;

                        switch (settings.Properties.ToolbarPosition.Value)
                        {
                            case "Top center":
                                Position = Position.Top;
                                break;
                            case "Bottom center":
                                Position = Position.Bottom;
                                break;
                            case "Left":
                                Position = Position.Left;
                                break;
                            case "Right":
                                Position = Position.Right;
                                break;
                            case "Top right corner":
                                Position = Position.TopRight;
                                break;
                            case "Top left corner":
                                Position = Position.TopLeft;
                                break;
                            case "Bottom right corner":
                                Position = Position.BottomRight;
                                break;
                            case "Bottom left corner":
                                Position = Position.BottomLeft;
                                break;
                            case "Center":
                                Position = Position.Center;
                                break;
                        }

                        ShowUnicodeDescription = settings.Properties.ShowUnicodeDescription;
                        SortByUsageFrequency = settings.Properties.SortByUsageFrequency;
                        StartSelectionFromTheLeft = settings.Properties.StartSelectionFromTheLeft;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to read changed settings", ex);
                }
            }
        }
    }

    private PowerAccentActivationKey _activationKey = PowerAccentActivationKey.Both;

    public PowerAccentActivationKey ActivationKey
    {
        get
        {
            return _activationKey;
        }

        set
        {
            _activationKey = value;
        }
    }

    private bool _doNotActivateOnGameMode = true;

    public bool DoNotActivateOnGameMode
    {
        get
        {
            return _doNotActivateOnGameMode;
        }

        set
        {
            _doNotActivateOnGameMode = value;
        }
    }

    private Position _position = Position.Top;

    public Position Position
    {
        get
        {
            return _position;
        }

        set
        {
            _position = value;
        }
    }

    private int _inputTime = PowerAccentSettings.DefaultInputTimeMs;

    public int InputTime
    {
        get
        {
            return _inputTime;
        }

        set
        {
            _inputTime = value;
        }
    }

    private string _excludedApps;

    public string ExcludedApps
    {
        get
        {
            return _excludedApps;
        }

        set
        {
            _excludedApps = value;
        }
    }

    private Language _selectedLang;

    public Language SelectedLang
    {
        get
        {
            return _selectedLang;
        }

        set
        {
            _selectedLang = value;
        }
    }

    private bool _showUnicodeDescription;

    public bool ShowUnicodeDescription
    {
        get
        {
            return _showUnicodeDescription;
        }

        set
        {
            _showUnicodeDescription = value;
        }
    }

    private bool _sortByUsageFrequency;

    public bool SortByUsageFrequency
    {
        get
        {
            return _sortByUsageFrequency;
        }

        set
        {
            _sortByUsageFrequency = value;
        }
    }

    private bool _startSelectionFromTheLeft;

    public bool StartSelectionFromTheLeft
    {
        get
        {
            return _startSelectionFromTheLeft;
        }

        set
        {
            _startSelectionFromTheLeft = value;
        }
    }
}

public enum Position
{
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}
