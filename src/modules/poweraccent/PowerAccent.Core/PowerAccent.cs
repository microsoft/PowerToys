// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;

namespace PowerAccent.Core;

public class PowerAccent : IDisposable
{
    private readonly SettingsService _settingService;

    private bool _visible;
    private char[] _characters = Array.Empty<char>();
    private int _selectedIndex = -1;

    public event Action<bool, char[]> OnChangeDisplay;

    public event Action<int, char> OnSelectCharacter;

    private PowerToys.PowerAccentKeyboardService.KeyboardListener _keyboardListener;

    public PowerAccent()
    {
        _keyboardListener = new PowerToys.PowerAccentKeyboardService.KeyboardListener();
        _keyboardListener.InitHook();
        _settingService = new SettingsService(_keyboardListener);

        SetEvents();
    }

    private void SetEvents()
    {
        _keyboardListener.SetShowToolbarEvent(new PowerToys.PowerAccentKeyboardService.ShowToolbar((PowerToys.PowerAccentKeyboardService.LetterKey letterKey) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowToolbar(letterKey);
            });
        }));

        _keyboardListener.SetHideToolbarEvent(new PowerToys.PowerAccentKeyboardService.HideToolbar((PowerToys.PowerAccentKeyboardService.InputType inputType) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SendInputAndHideToolbar(inputType);
            });
        }));

        _keyboardListener.SetNextCharEvent(new PowerToys.PowerAccentKeyboardService.NextChar((PowerToys.PowerAccentKeyboardService.TriggerKey triggerKey) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessNextChar(triggerKey);
            });
        }));
    }

    private void ShowToolbar(PowerToys.PowerAccentKeyboardService.LetterKey letterKey)
    {
        _visible = true;
        _characters = WindowsFunctions.IsCapitalState() ? ToUpper(SettingsService.GetDefaultLetterKey(letterKey)) : SettingsService.GetDefaultLetterKey(letterKey);
        Task.Delay(_settingService.InputTime).ContinueWith(
            t =>
            {
                if (_visible)
                {
                    OnChangeDisplay?.Invoke(true, _characters);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SendInputAndHideToolbar(PowerToys.PowerAccentKeyboardService.InputType inputType)
    {
        switch (inputType)
        {
            case PowerToys.PowerAccentKeyboardService.InputType.Space:
                {
                    WindowsFunctions.Insert(' ');
                    break;
                }

            case PowerToys.PowerAccentKeyboardService.InputType.Char:
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

    private void ProcessNextChar(PowerToys.PowerAccentKeyboardService.TriggerKey triggerKey)
    {
        if (_visible && _selectedIndex == -1)
        {
            if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Left)
            {
                _selectedIndex = (_characters.Length / 2) - 1;
            }

            if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Right)
            {
                _selectedIndex = _characters.Length / 2;
            }

            if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Space)
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
        }

        if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Space)
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

        if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Left && _selectedIndex > 0)
        {
            --_selectedIndex;
        }

        if (triggerKey == PowerToys.PowerAccentKeyboardService.TriggerKey.Right && _selectedIndex < _characters.Length - 1)
        {
            ++_selectedIndex;
        }

        OnSelectCharacter?.Invoke(_selectedIndex, _characters[_selectedIndex]);
    }

    public Point GetDisplayCoordinates(Size window)
    {
        var activeDisplay = WindowsFunctions.GetActiveDisplay();
        Rect screen = new Rect(activeDisplay.Location, activeDisplay.Size) / activeDisplay.Dpi;
        Position position = _settingService.Position;

        /* Debug.WriteLine("Dpi: " + activeDisplay.Dpi); */

        return Calculation.GetRawCoordinatesFromPosition(position, screen, window);
    }

    public void Dispose()
    {
        _keyboardListener.UnInitHook();
        GC.SuppressFinalize(this);
    }

    public static char[] ToUpper(char[] array)
    {
        char[] result = new char[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            result[i] = char.ToUpper(array[i], System.Globalization.CultureInfo.InvariantCulture);
        }

        return result;
    }
}
