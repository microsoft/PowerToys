// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interop;
using PeekUI.Native;
using static PeekUI.Native.NativeModels;

namespace PeekUI.Extensions
{
    public static class WindowExtensions
    {
        public static void SetToolStyle(this Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            _ = NativeMethods.SetWindowLong(handle, GwlExStyle, NativeMethods.GetWindowLong(handle, GwlExStyle) | WsExToolWindow);
        }

        public static void BringToForeground(this Window window)
        {
            // Use SendInput hack to allow Activate to work - required to resolve focus issue https://github.com/microsoft/PowerToys/issues/4270
            Input input = new Input { Type = InputType.InputMouse, Data = { } };
            Input[] inputs = new Input[] { input };

            // Send empty mouse event. This makes this thread the last to send input, and hence allows it to pass foreground permission checks
            _ = NativeMethods.SendInput(1, inputs, Input.Size);

            window.Activate();
        }

        public static void RoundCorners(this Window window)
        {
            IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(window)).EnsureHandle();
            var attribute = DwmWindowAttributed.DwmaWindowCornerPreference;
            var preference = DwmWindowCornerPreference.DwmCpRound;
            NativeMethods.DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));
        }
    }
}
