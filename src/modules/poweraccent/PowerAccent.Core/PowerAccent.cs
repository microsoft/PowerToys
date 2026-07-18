// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

using ManagedCommon;
using PowerAccent.Common;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;
using PowerToys.PowerAccentKeyboardService;

using LetterKey = PowerToys.PowerAccentKeyboardService.LetterKey;
using PowerAccentActivationKey = Microsoft.PowerToys.Settings.UI.Library.Enumerations.PowerAccentActivationKey;

namespace PowerAccent.Core;

public partial class PowerAccent : IDisposable
{
    private readonly SettingsService _settingService;

    // Keys that show a description (like dashes) when ShowCharacterInfoSetting is 1
    private readonly LetterKey[] _letterKeysShowingDescription = new LetterKey[] { LetterKey.VK_O };
    private const double ScreenMinPadding = 150;

    private bool _visible;
    private int _showGeneration;
    private string[] _characters = Array.Empty<string>();
    private string[] _characterDescriptions = Array.Empty<string>();
    private int _selectedIndex = -1;
    private bool _showUnicodeDescription;
    private bool _initialShiftState; // Was shift held down when the toolbar was summoned?

    public LetterKey[] LetterKeysShowingDescription => _letterKeysShowingDescription;

    public bool ShowUnicodeDescription => _showUnicodeDescription;

    public string[] CharacterDescriptions => _characterDescriptions;

    public event Action<bool, string[]> OnChangeDisplay;

    public event Action<int, string> OnSelectCharacter;

    private readonly KeyboardListener _keyboardListener;

    private readonly CharactersUsageInfo _usageInfo;

    private readonly Action<Action> _runOnUiThread;

    public PowerAccent(Action<Action> runOnUiThread)
    {
        _runOnUiThread = runOnUiThread ?? throw new ArgumentNullException(nameof(runOnUiThread));

        Logger.InitializeLogger("\\QuickAccent\\Logs");

        _keyboardListener = new KeyboardListener();
        _keyboardListener.InitHook();
        _settingService = new SettingsService(_keyboardListener);
        _usageInfo = new CharactersUsageInfo();

        SetEvents();
    }

    private void SetEvents()
    {
        _keyboardListener.SetShowToolbarEvent(new PowerToys.PowerAccentKeyboardService.ShowToolbar((LetterKey letterKey) =>
        {
            _runOnUiThread(() =>
            {
                ShowToolbar(letterKey);
            });
        }));

        _keyboardListener.SetHideToolbarEvent(new PowerToys.PowerAccentKeyboardService.HideToolbar((InputType inputType) =>
        {
            _runOnUiThread(() =>
            {
                SendInputAndHideToolbar(inputType);
            });
        }));

        _keyboardListener.SetNextCharEvent(new PowerToys.PowerAccentKeyboardService.NextChar((TriggerKey triggerKey, bool shiftPressed) =>
        {
            _runOnUiThread(() =>
            {
                ProcessNextChar(triggerKey, shiftPressed);
            });
        }));

        _keyboardListener.SetIsLanguageLetterDelegate(new PowerToys.PowerAccentKeyboardService.IsLanguageLetter((LetterKey letterKey, out bool result) =>
        {
            result = CharacterMappings.GetCharacters(letterKey, _settingService.SelectedLang).Length > 0;
        }));
    }

    private void ShowToolbar(LetterKey letterKey)
    {
        _visible = true;

        bool isPressAndHold = _settingService.ActivationKey == PowerAccentActivationKey.PressAndHold;

        // Each summon gets a generation id so a delayed render queued by an earlier
        // press can't fire for a newer one (or after the toolbar was hidden).
        int generation = ++_showGeneration;

        // Trigger modes navigate the instant the toolbar is summoned, so the character data must
        // be ready synchronously. Press-and-hold can't navigate until the popup is actually shown,
        // so defer the (relatively expensive) character/description build to the delayed render and
        // keep quick taps off the keystroke hot path.
        if (!isPressAndHold)
        {
            PrepareCharacters(letterKey);
        }

        int displayDelay = isPressAndHold ? _settingService.HoldDuration : _settingService.InputTime;

        Task.Delay(displayDelay).ContinueWith(
        t =>
        {
            if (_visible && generation == _showGeneration)
            {
                if (isPressAndHold)
                {
                    PrepareCharacters(letterKey);
                }

                OnChangeDisplay?.Invoke(true, _characters);
            }
        },
        TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void PrepareCharacters(LetterKey letterKey)
    {
        _initialShiftState = WindowsFunctions.IsShiftState();
        _characters = GetCharacters(letterKey);
        _characterDescriptions = GetCharacterDescriptions(_characters);
        _showUnicodeDescription = _settingService.ShowUnicodeDescription;
    }

    private string[] GetCharacters(LetterKey letterKey)
    {
        var characters = CharacterMappings.GetCharacters(letterKey, _settingService.SelectedLang);
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
        // TODO: Zero-width joiners (U+200D) and variation selectors (U+FE0F, etc.) currently show
        // up as their own entries in the description (e.g. for complex emoji sequences). In the
        // future, when we support arbitrary user-defined sequences, we will want to filter these
        // out for display purposes, since they're not meaningful to users even though they're
        // technically part of the code point sequence.
        if (string.IsNullOrEmpty(character))
        {
            return string.Empty;
        }

        // Enumerate code points manually to handle surrogate pairs and combining sequences
        // correctly. For example, "°C" is a two-code-point string (U+00B0 + U+0043) but should
        // be treated as a single character for description purposes.
        var codePointInfo = new List<(int CodePoint, string Str, string Name)>();
        for (int i = 0; i < character.Length;)
        {
            int codePoint = char.ConvertToUtf32(character, i);
            string codePointString = char.ConvertFromUtf32(codePoint);
            string name = UnicodeHelper.GetCharacterName(codePointString) ?? string.Empty;
            codePointInfo.Add((codePoint, codePointString, name));
            i += char.IsHighSurrogate(character[i]) ? 2 : 1;
        }

        if (codePointInfo.Count == 1)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "(U+{0:X4}) {1}", codePointInfo[0].CodePoint, codePointInfo[0].Name);
        }

        // Multiple code points. Build the description string with each code point's information.
        string displayTextAndCodes = string.Join(" - ", codePointInfo.Select(info =>
            string.Format(CultureInfo.InvariantCulture, "{0}: (U+{1:X4})", info.Str, info.CodePoint)));

        string names = string.Join(", ", codePointInfo.Select(info => info.Name));

        return string.Format(CultureInfo.InvariantCulture, "{0}: {1}", displayTextAndCodes, names);
    }

    private string[] GetCharacterDescriptions(string[] characters) =>
        Array.ConvertAll(characters, GetCharacterDescription);

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
                    WindowsFunctions.SendArrowKey(left: false);
                    break;
                }

            case InputType.Left:
                {
                    WindowsFunctions.SendArrowKey(left: true);
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
        _showGeneration++;
    }

    private void ProcessNextChar(TriggerKey triggerKey, bool shiftPressed)
    {
        // Press-and-hold builds its character set lazily when the popup renders; ignore any
        // navigation that races ahead of it (there is nothing to select yet).
        if (_characters.Length == 0)
        {
            return;
        }

        // Use an async hardware check as a fallback in case the keyboard hook misses a
        // quick Shift press. If the popup was opened while holding Shift (e.g., typing a
        // capital letter), ignore the hardware check so we don't accidentally trigger a
        // backwards navigation.
        bool isHardwareShiftPressed = WindowsFunctions.IsShiftState() && !_initialShiftState;
        shiftPressed = shiftPressed || isHardwareShiftPressed;

        if (_visible && _selectedIndex == -1)
        {
            if (triggerKey == TriggerKey.Space)
            {
                _selectedIndex = shiftPressed ? (_characters.Length - 1) : 0;
            }
            else if (_settingService.StartSelectionFromTheLeft)
            {
                _selectedIndex = 0;
            }
            else if (triggerKey == TriggerKey.Left)
            {
                _selectedIndex = (_characters.Length / 2) - 1;
            }
            else if (triggerKey == TriggerKey.Right)
            {
                _selectedIndex = _characters.Length / 2;
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

    /// <summary>
    /// Calculates the coordinates at which a window of the specified size should be
    /// displayed, based on the current display settings and user preferences.
    /// </summary>
    /// <remarks>The calculated coordinates take into account the active display's
    /// location, size, DPI, and the user's configured position preferences.</remarks>
    /// <param name="window">The size of the window for which to calculate display
    /// coordinates.</param>
    /// <returns>A point representing the top-left coordinates where the window should be
    /// positioned on the active display, in physical/raw coordinates suitable for Win32
    /// APIs like SetWindowPos.</returns>
    public Point GetDisplayCoordinates(Size window)
    {
        (Point Location, Size Size, double Dpi) activeDisplay = WindowsFunctions.GetActiveDisplay();
        Rect screen = new(activeDisplay.Location, activeDisplay.Size);
        Position position = _settingService.Position;

        return Calculation.GetRawCoordinatesFromPosition(position, screen, window, activeDisplay.Dpi);
    }

    /// <summary>
    /// Gets the maximum width for the toolbar display based on the active screen
    /// dimensions.
    /// </summary>
    /// <returns>The maximum width in DIPs (device-independent pixels), accounting for
    /// screen padding.</returns>
    public double GetDisplayMaxWidth()
    {
        // activeDisplay.Size.Width is in raw physical pixels; divide by the DPI scale to
        // convert to DIPs (device-independent pixels), since ScreenMinPadding and the
        // consuming window width are both expressed in DIPs.
        var activeDisplay = WindowsFunctions.GetActiveDisplay();
        return (activeDisplay.Size.Width / activeDisplay.Dpi) - ScreenMinPadding;
    }

    /// <summary>
    /// Gets the user-configured position preference for the toolbar display. For example
    /// <see cref="Position.TopLeft"/>.
    /// </summary>
    /// <returns>The preferred location for the toolbar.</returns>
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
        List<string> result = new(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            switch (array[i])
            {
                case "ß": result.Add("ẞ"); break;
                case "ǰ": result.Add("J\u030c"); break;
                case "ı\u0307\u0304": result.Add("İ\u0304"); break;
                case "ı": result.Add("İ"); break;
                case "ᵛ": result.Add("ⱽ"); break;
                case "ⁿ": result.Add("ᴺ"); break;
                case "ϑ": break;
                default: result.Add(array[i].ToUpper(CultureInfo.InvariantCulture)); break;
            }
        }

        return [..result];
    }
}
