// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using KeyboardManagerEditorUI.Helpers;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;

namespace KeyboardManagerEditorUI
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            this.InitializeComponent();
            SetTitleBar();
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }

        private void SetTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            this.SetIcon(@"Assets\KeyboardManagerEditor\Keyboard.ico");
            this.SetTitleBar(titleBar);
            TitleBarHelper.SetPreferredTheme(this);

            Title = "Keyboard Manager";
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                // Release the keyboard hook when the window is deactivated
                KeyboardHookHelper.Instance.CleanupHook();
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            KeyboardHookHelper.Instance.Dispose();
            this.Activated -= MainWindow_Activated;
            this.Closed -= MainWindow_Closed;
        }

        private void SearchBox_FindInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        }
    }
}
