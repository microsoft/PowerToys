// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Dispatching;
using ScreenTranslator.Helpers;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace ScreenTranslator.Keyboard;

public sealed class KeyboardMonitor : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private GlobalKeyboardHook? _keyboardHook;
    private bool _winPressed;
    private bool _ctrlPressed;

    public KeyboardMonitor()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public void Start()
    {
        _keyboardHook = new GlobalKeyboardHook();
        _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;
    }

    private void Hook_KeyboardPressed(object? sender, GlobalKeyboardHookEventArgs e)
    {
        if (e.Key == VirtualKey.LeftWindows || e.Key == VirtualKey.RightWindows)
        {
            _winPressed = e.IsKeyDown;
        }
        else if (e.Key == VirtualKey.Control || e.Key == VirtualKey.LeftControl || e.Key == VirtualKey.RightControl)
        {
            _ctrlPressed = e.IsKeyDown;
        }
        else if (e.Key == VirtualKey.T && e.IsKeyDown)
        {
            if (_winPressed && _ctrlPressed)
            {
                e.Handled = true;
                _dispatcherQueue.TryEnqueue(() =>
                {
                    WindowManager.LaunchScreenTranslatorOnEveryScreen();
                });
            }
        }
        else if (e.Key == VirtualKey.Escape && e.IsKeyDown)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                WindowManager.CloseAllOverlays();
            });
        }
    }

    public void Dispose()
    {
        _keyboardHook?.Dispose();
        _keyboardHook = null;
        GC.SuppressFinalize(this);
    }
}
