// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Pages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI;

public sealed partial class MainWindow
{
    /// <summary>
    /// Coordinates shortcut dispatch and native display mode with CmdPal's access-key state.
    /// </summary>
    private sealed partial class AccessKeyInputHandler : IDisposable
    {
        private readonly MainWindow _window;
        private readonly AccessKeyModeController _accessKeyMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyInputHandler"/> class.
        /// </summary>
        /// <param name="window">The window hosting the input handler.</param>
        /// <param name="accessKeyMode">The shared access-key state.</param>
        public AccessKeyInputHandler(MainWindow window, AccessKeyModeController accessKeyMode)
        {
            _window = window;
            _accessKeyMode = accessKeyMode;
            _accessKeyMode.ExitRequested += AccessKeyMode_ExitRequested;
            _window._localKeyboardListener.KeyStateChanged += LocalKeyboardListener_OnKeyStateChanged;
            AccessKeyManager.IsDisplayModeEnabledChanged += AccessKeyManager_IsDisplayModeEnabledChanged;
        }

        private bool IsInputActive => _window._localKeyboardListener.EnableRaisingEvents && _window.IsVisibleToUser;

        /// <summary>
        /// Unsubscribes from keyboard input and native display-mode changes.
        /// </summary>
        public void Dispose()
        {
            _accessKeyMode.ExitRequested -= AccessKeyMode_ExitRequested;
            _window._localKeyboardListener.KeyStateChanged -= LocalKeyboardListener_OnKeyStateChanged;
            AccessKeyManager.IsDisplayModeEnabledChanged -= AccessKeyManager_IsDisplayModeEnabledChanged;
        }

        private void LocalKeyboardListener_OnKeyStateChanged(object? sender, LocalKeyboardListenerKeyStateChangedEventArgs e)
        {
            if (!e.IsDown)
            {
                _accessKeyMode.HandleKeyUp(e.Key);
                return;
            }

            var modifiers = KeyModifiers.GetCurrent();
            var chord = KeyChordHelpers.FromModifiers(
                ctrl: modifiers.Ctrl,
                alt: modifiers.Alt,
                shift: modifiers.Shift,
                win: modifiers.Win,
                vkey: e.Key);
            var exitGeneration = _accessKeyMode.HandleKeyDown(chord);

            if (_window.RootElement.MainContent is ShellPage shellPage && shellPage.TryHandleAccessKey(chord))
            {
                if (modifiers.Alt && IsInputActive)
                {
                    _accessKeyMode.SuppressNativeDisplayMode();
                }

                AccessKeyManager.ExitDisplayMode();
                e.Handled = true;
            }

            if (exitGeneration is long generation)
            {
                QueueAccessKeyModeExit(generation);
            }
        }

        private void AccessKeyMode_ExitRequested(object? sender, EventArgs e) => AccessKeyManager.ExitDisplayMode();

        private void AccessKeyManager_IsDisplayModeEnabledChanged(object? sender, object args)
        {
            // A swallowed shortcut can make WinUI treat Alt up as a bare Alt tap.
            if (IsInputActive && _accessKeyMode.IsNativeDisplayModeSuppressed && AccessKeyManager.IsDisplayModeEnabled)
            {
                AccessKeyManager.ExitDisplayMode();
            }
            else
            {
                _accessKeyMode.HandleNativeDisplayModeChanged(IsInputActive && AccessKeyManager.IsDisplayModeEnabled);
            }
        }

        private void QueueAccessKeyModeExit(long generation)
        {
            // WinUI can keep its mode active after an unmatched key or a partial access-key match.
            // Check after dispatch so both sets of cues follow the native result.
            if (!_window.DispatcherQueue.TryEnqueue(() => _accessKeyMode.ExitIfCurrent(generation, AccessKeyManager.IsDisplayModeEnabled)))
            {
                _accessKeyMode.ExitIfCurrent(generation, AccessKeyManager.IsDisplayModeEnabled);
            }
        }
    }
}
