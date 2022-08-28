// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core.Services;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using System.IO.Abstractions;
using System.Text.Json;

public class SettingsService
{
    private const string PowerAccentModuleName = "PowerAccent";
    private readonly ISettingsUtils _settingsUtils;
    private readonly IFileSystemWatcher _watcher;
    private readonly object _loadingSettingsLock = new object();

    public SettingsService()
    {
        _settingsUtils = new SettingsUtils();
        ReadSettings();
        _watcher = Helper.GetFileWatcher(PowerAccentModuleName, "settings.json", () => { ReadSettings(); });
    }

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
                        Logger.LogInfo("PowerAccent settings.json was missing, creating a new one");
                        var defaultSettings = new PowerAccentSettings();
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                        };

                        _settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), PowerAccentModuleName);
                    }

                    var settings = _settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentModuleName);
                    if (settings != null)
                    {
                        ActivationKey = settings.Properties.ActivationKey;
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

    private int _inputTime = 200;

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

    public char[] GetLetterKey(LetterKey letter)
    {
        return GetDefaultLetterKey(letter);
    }

    public static char[] GetDefaultLetterKey(LetterKey letter)
    {
        switch (letter)
        {
            case LetterKey.A:
                return new char[] { 'à', 'â', 'á', 'ä', 'ã', 'å', 'æ' };
            case LetterKey.C:
                return new char[] { 'ć', 'ĉ', 'č', 'ċ', 'ç', 'ḉ' };
            case LetterKey.E:
                return new char[] { 'é', 'è', 'ê', 'ë', 'ē', 'ė', '€' };
            case LetterKey.I:
                return new char[] { 'î', 'ï', 'í', 'ì', 'ī' };
            case LetterKey.N:
                return new char[] { 'ñ', 'ń' };
            case LetterKey.O:
                return new char[] { 'ô', 'ö', 'ó', 'ò', 'õ', 'ø', 'œ' };
            case LetterKey.S:
                return new char[] { 'š', 'ß', 'ś' };
            case LetterKey.U:
                return new char[] { 'û', 'ù', 'ü', 'ú', 'ū' };
            case LetterKey.Y:
                return new char[] { 'ÿ', 'ý' };
        }

        throw new ArgumentException("Letter {0} is missing", letter.ToString());
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
