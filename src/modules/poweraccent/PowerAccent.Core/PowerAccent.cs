// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;
using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core;

public class PowerAccent : IDisposable
{
    private readonly SettingsService _settingService;

    private bool _visible;

    private string[] _characters = Array.Empty<string>();

    private int _selectedIndex = -1;

    public event Action<bool, string[]> OnChangeDisplay;

    public event Action<int, string> OnSelectCharacter;

    private KeyboardListener _keyboardListener;

    public PowerAccent()
    {
        _keyboardListener = new KeyboardListener();
        _keyboardListener.InitHook();
        _settingService = new SettingsService(_keyboardListener);

        SetEvents();
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
        _characters = (WindowsFunctions.IsCapsLockState() || WindowsFunctions.IsShiftState()) ? ToUpper(Languages.GetDefaultLetterKey(letterKey, _settingService.SelectedLang)) : Languages.GetDefaultLetterKey(letterKey, _settingService.SelectedLang);
        Task.Delay(_settingService.InputTime).ContinueWith(
            t =>
            {
                if (_visible)
                {
                    OnChangeDisplay?.Invoke(true, _characters);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
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

            case InputType.Char:
                {
                    if (_selectedIndex != -1)
                    {
                        WindowsFunctions.Insert(_characters[_selectedIndex], true);
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

            if (triggerKey == TriggerKey.Space)
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
            if (array[i].Contains('ß'))
            {
                result[i] = "ẞ";
            }
            else
            {
                result[i] = array[i].ToUpper(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return result;
    }
}
