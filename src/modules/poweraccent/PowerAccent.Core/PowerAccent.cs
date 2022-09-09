// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;

namespace PowerAccent.Core;

public class PowerAccent : IDisposable
{
    private readonly SettingsService _settingService = new SettingsService();
    private readonly KeyboardListener _keyboardListener = new KeyboardListener();

    private LetterKey? letterPressed;
    private bool _visible;
    private char[] _characters = Array.Empty<char>();
    private int _selectedIndex = -1;
    private Stopwatch _stopWatch;
    private bool _triggeredWithSpace;

    public event Action<bool, char[]> OnChangeDisplay;

    public event Action<int, char> OnSelectCharacter;

    public PowerAccent()
    {
        _keyboardListener.KeyDown += PowerAccent_KeyDown;
        _keyboardListener.KeyUp += PowerAccent_KeyUp;
    }

    private bool PowerAccent_KeyDown(object sender, KeyboardListener.RawKeyEventArgs args)
    {
        if (Enum.IsDefined(typeof(LetterKey), (int)args.Key))
        {
            _stopWatch = Stopwatch.StartNew();
            letterPressed = (LetterKey)args.Key;
        }

        TriggerKey? triggerPressed = null;
        if (letterPressed.HasValue)
        {
            if (Enum.IsDefined(typeof(TriggerKey), (int)args.Key))
            {
                triggerPressed = (TriggerKey)args.Key;

                if ((triggerPressed == TriggerKey.Space && _settingService.ActivationKey == PowerAccentActivationKey.LeftRightArrow) ||
                    ((triggerPressed == TriggerKey.Left || triggerPressed == TriggerKey.Right) && _settingService.ActivationKey == PowerAccentActivationKey.Space))
                {
                    triggerPressed = null;
                }
            }
        }

        if (!_visible && letterPressed.HasValue && triggerPressed.HasValue)
        {
            // Keep track if it was triggered with space so that it can be typed on false starts.
            _triggeredWithSpace = triggerPressed.Value == TriggerKey.Space;
            _visible = true;
            _characters = WindowsFunctions.IsCapitalState() ? ToUpper(_settingService.GetLetterKey(letterPressed.Value)) : _settingService.GetLetterKey(letterPressed.Value);
            Task.Delay(_settingService.InputTime).ContinueWith(
                t =>
                {
                    if (_visible)
                    {
                        OnChangeDisplay?.Invoke(true, _characters);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        if (_visible && triggerPressed.HasValue)
        {
            if (_selectedIndex == -1)
            {
                if (triggerPressed.Value == TriggerKey.Left)
                {
                    _selectedIndex = (_characters.Length / 2) - 1;
                }

                if (triggerPressed.Value == TriggerKey.Right)
                {
                    _selectedIndex = _characters.Length / 2;
                }

                if (triggerPressed.Value == TriggerKey.Space)
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
                return false;
            }

            if (triggerPressed.Value == TriggerKey.Space)
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

            if (triggerPressed.Value == TriggerKey.Left && _selectedIndex > 0)
            {
                --_selectedIndex;
            }

            if (triggerPressed.Value == TriggerKey.Right && _selectedIndex < _characters.Length - 1)
            {
                ++_selectedIndex;
            }

            OnSelectCharacter?.Invoke(_selectedIndex, _characters[_selectedIndex]);
            return false;
        }

        return true;
    }

    private bool PowerAccent_KeyUp(object sender, KeyboardListener.RawKeyEventArgs args)
    {
        if (Enum.IsDefined(typeof(LetterKey), (int)args.Key))
        {
            letterPressed = null;
            _stopWatch.Stop();
            if (_visible)
            {
                if (_stopWatch.ElapsedMilliseconds < _settingService.InputTime)
                {
                    /* Debug.WriteLine("Insert before inputTime - " + _stopWatch.ElapsedMilliseconds); */

                    // False start, we should output the space if it was the trigger.
                    if (_triggeredWithSpace)
                    {
                        WindowsFunctions.Insert(' ');
                    }

                    OnChangeDisplay?.Invoke(false, null);
                    _selectedIndex = -1;
                    _visible = false;
                    return false;
                }

                /* Debug.WriteLine("Insert after inputTime - " + _stopWatch.ElapsedMilliseconds); */
                OnChangeDisplay?.Invoke(false, null);
                if (_selectedIndex != -1)
                {
                    WindowsFunctions.Insert(_characters[_selectedIndex], true);
                }

                _selectedIndex = -1;
                _visible = false;
            }
        }

        return true;
    }

    public Point GetDisplayCoordinates(Size window)
    {
        var activeDisplay = WindowsFunctions.GetActiveDisplay();
        Rect screen = new Rect(activeDisplay.Location, activeDisplay.Size) / activeDisplay.Dpi;
        Position position = _settingService.Position;

        /* Debug.WriteLine("Dpi: " + activeDisplay.Dpi); */

        return Calculation.GetRawCoordinatesFromPosition(position, screen, window);
    }

    public char[] GetLettersFromKey(LetterKey letter)
    {
        return _settingService.GetLetterKey(letter);
    }

    public void Dispose()
    {
        _keyboardListener.Dispose();
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
