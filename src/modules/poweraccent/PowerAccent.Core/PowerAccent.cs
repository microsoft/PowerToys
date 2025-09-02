// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Unicode;
using System.Windows;

using ManagedCommon;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;
using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core;

public partial class PowerAccent : IDisposable
{
    private readonly SettingsService _settingService;

    // Keys that show a description (like dashes) when ShowCharacterInfoSetting is 1
    private readonly LetterKey[] _letterKeysShowingDescription = new LetterKey[] { LetterKey.VK_O };

    private bool _visible;
    private string[] _characters = Array.Empty<string>();
    private string[] _characterDescriptions = Array.Empty<string>();
    private int _selectedIndex = -1;
    private bool _showUnicodeDescription;

    public LetterKey[] LetterKeysShowingDescription => _letterKeysShowingDescription;

    public bool ShowUnicodeDescription => _showUnicodeDescription;

    public string[] CharacterDescriptions => _characterDescriptions;

    public event Action<bool, string[]> OnChangeDisplay;

    public event Action<int, string> OnSelectCharacter;

    private readonly KeyboardListener _keyboardListener;

    private readonly CharactersUsageInfo _usageInfo;

    public PowerAccent()
    {
        Logger.InitializeLogger("\\QuickAccent\\Logs");

        LoadUnicodeInfoCache();

        _keyboardListener = new KeyboardListener();
        _keyboardListener.InitHook();
        _settingService = new SettingsService(_keyboardListener);
        _usageInfo = new CharactersUsageInfo();

        SetEvents();
    }

    private void LoadUnicodeInfoCache()
    {
        UnicodeInfo.GetCharInfo(0);
    }

    private void SetEvents()
    {
        _keyboardListener.SetShowToolbarEvent(new PowerToys.PowerAccentKeyboardService.ShowToolbar((LetterKey letterKey) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ShowToolbar(letterKey);
            });
        }));

        _keyboardListener.SetHideToolbarEvent(new PowerToys.PowerAccentKeyboardService.HideToolbar((InputType inputType) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SendInputAndHideToolbar(inputType);
            });
        }));

        _keyboardListener.SetNextCharEvent(new PowerToys.PowerAccentKeyboardService.NextChar((TriggerKey triggerKey, bool shiftPressed) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessNextChar(triggerKey, shiftPressed);
            });
        }));

        _keyboardListener.SetIsLanguageLetterDelegate(new PowerToys.PowerAccentKeyboardService.IsLanguageLetter((LetterKey letterKey, out bool result) =>
        {
            result = Languages.GetDefaultLetterKey(letterKey, _settingService.SelectedLang).Length > 0;
        }));
    }

    private void ShowToolbar(LetterKey letterKey)
    {
        _visible = true;

        _characters = GetCharacters(letterKey);
        _characterDescriptions = GetCharacterDescriptions(_characters);
        _showUnicodeDescription = _settingService.ShowUnicodeDescription;

        Task.Delay(_settingService.InputTime).ContinueWith(
        t =>
        {
            if (_visible)
            {
                OnChangeDisplay?.Invoke(true, _characters);
            }
        },
        TaskScheduler.FromCurrentSynchronizationContext());
    }

    private string[] GetCharacters(LetterKey letterKey)
    {
        var characters = Languages.GetDefaultLetterKey(letterKey, _settingService.SelectedLang);
        if (_settingService.SortByUsageFrequency)
        {
            characters = characters.OrderByDescending(character => _usageInfo.GetUsageFrequency(character))
                .ThenByDescending(character => _usageInfo.GetLastUsageTimestamp(character))
                .ToArray<string>();
        }
        else if (!_usageInfo.Empty())
        {
            _usageInfo.Clear();
        }

        if (WindowsFunctions.IsCapsLockState() || WindowsFunctions.IsShiftState())
        {
            return ToUpper(characters);
        }

        return characters;
    }

    private string GetCharacterDescription(string character)
    {
        List<UnicodeCharInfo> unicodeList = new List<UnicodeCharInfo>();
        foreach (var codePoint in character.AsCodePointEnumerable())
        {
            unicodeList.Add(UnicodeInfo.GetCharInfo(codePoint));
        }

        if (unicodeList.Count == 0)
        {
            return string.Empty;
        }

        var description = new StringBuilder();
        if (unicodeList.Count == 1)
        {
            var unicode = unicodeList.First();
            var charUnicodeNumber = unicode.CodePoint.ToString("X4", CultureInfo.InvariantCulture);
            description.AppendFormat(CultureInfo.InvariantCulture, "(U+{0}) {1}", charUnicodeNumber, unicode.Name);

            return description.ToString();
        }

        var displayTextAndCodes = new StringBuilder();
        var names = new StringBuilder();
        foreach (var unicode in unicodeList)
        {
            var charUnicodeNumber = unicode.CodePoint.ToString("X4", CultureInfo.InvariantCulture);
            if (displayTextAndCodes.Length > 0)
            {
                displayTextAndCodes.Append(" - ");
            }

            displayTextAndCodes.AppendFormat(CultureInfo.InvariantCulture, "{0}: (U+{1})", unicode.GetDisplayText(), charUnicodeNumber);

            if (names.Length > 0)
            {
                names.Append(", ");
            }

            names.Append(unicode.Name);
        }

        description.Append(displayTextAndCodes);
        description.Append(": ");
        description.Append(names);

        return description.ToString();
    }

    private string[] GetCharacterDescriptions(string[] characters)
    {
        string[] charInfoCollection = Array.Empty<string>();
        foreach (string character in characters)
        {
            charInfoCollection = charInfoCollection.Append<string>(GetCharacterDescription(character)).ToArray<string>();
        }

        return charInfoCollection;
    }

    private void SendInputAndHideToolbar(InputType inputType)
    {
        switch (inputType)
        {
            case InputType.Space:
                {
                    WindowsFunctions.Insert(" ");
                    break;
                }

            case InputType.Right:
                {
                    SendKeys.SendWait("{RIGHT}");
                    break;
                }

            case InputType.Left:
                {
                    SendKeys.SendWait("{LEFT}");
                    break;
                }

            case InputType.Char:
                {
                    if (_selectedIndex != -1)
                    {
                        WindowsFunctions.Insert(_characters[_selectedIndex], true);

                        if (_settingService.SortByUsageFrequency)
                        {
                            _usageInfo.IncrementUsageFrequency(_characters[_selectedIndex]);
                        }
                    }

                    break;
                }
        }

        OnChangeDisplay?.Invoke(false, null);
        _selectedIndex = -1;
        _visible = false;
    }

    private void ProcessNextChar(TriggerKey triggerKey, bool shiftPressed)
    {
        if (_visible && _selectedIndex == -1)
        {
            if (triggerKey == TriggerKey.Left)
            {
                _selectedIndex = (_characters.Length / 2) - 1;
            }

            if (triggerKey == TriggerKey.Right)
            {
                _selectedIndex = _characters.Length / 2;
            }

            if (triggerKey == TriggerKey.Space || _settingService.StartSelectionFromTheLeft)
            {
                _selectedIndex = 0;
            }

            if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
            }

            if (_selectedIndex > _characters.Length - 1)
            {
                _selectedIndex = _characters.Length - 1;
            }

            OnSelectCharacter?.Invoke(_selectedIndex, _characters[_selectedIndex]);
            return;
        }

        if (triggerKey == TriggerKey.Space)
        {
            if (shiftPressed)
            {
                if (_selectedIndex == 0)
                {
                    _selectedIndex = _characters.Length - 1;
                }
                else
                {
                    --_selectedIndex;
                }
            }
            else
            {
                if (_selectedIndex < _characters.Length - 1)
                {
                    ++_selectedIndex;
                }
                else
                {
                    _selectedIndex = 0;
                }
            }
        }

        if (triggerKey == TriggerKey.Left)
        {
            --_selectedIndex;
        }

        if (triggerKey == TriggerKey.Right)
        {
            ++_selectedIndex;
        }

        // Wrap around at beginning and end of _selectedIndex range
        if (_selectedIndex < 0)
        {
            _selectedIndex = _characters.Length - 1;
        }

        if (_selectedIndex > _characters.Length - 1)
        {
            _selectedIndex = 0;
        }

        OnSelectCharacter?.Invoke(_selectedIndex, _characters[_selectedIndex]);
    }

    public Point GetDisplayCoordinates(Size window)
    {
        (Point Location, Size Size, double Dpi) activeDisplay = WindowsFunctions.GetActiveDisplay();
        double primaryDPI = Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth;
        Rect screen = new Rect(activeDisplay.Location, activeDisplay.Size) / primaryDPI;
        Position position = _settingService.Position;

        /* Debug.WriteLine("Dpi: " + activeDisplay.Dpi); */

        return Calculation.GetRawCoordinatesFromPosition(position, screen, window);
    }

    public Position GetToolbarPosition()
    {
        return _settingService.Position;
    }

    public void SaveUsageInfo()
    {
        if (_settingService.SortByUsageFrequency)
        {
            _usageInfo.Save();
        }
    }

    public void Dispose()
    {
        _keyboardListener.UnInitHook();
        GC.SuppressFinalize(this);
    }

    public static string[] ToUpper(string[] array)
    {
        string[] result = new string[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            switch (array[i])
            {
                case "ß": result[i] = "ẞ"; break;
                case "ǰ": result[i] = "J\u030c"; break;
                case "ı\u0307\u0304": result[i] = "İ\u0304"; break;
                case "ı": result[i] = "İ"; break;
                case "ᵛ": result[i] = "ⱽ"; break;
                default: result[i] = array[i].ToUpper(System.Globalization.CultureInfo.InvariantCulture); break;
            }
        }

        return result;
    }
}
