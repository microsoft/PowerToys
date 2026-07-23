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
using Microsoft.UI;
using Microsoft.UI.Windowing;
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
        private const int ErrorAlreadyInitialized = unchecked((int)0x800704DF);

        public MainWindow()
        {
            this.InitializeComponent();
            SetTitleBar();
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }

        private void SetTitleBar()
        {
            this.SetIcon(@"Assets\KeyboardManagerEditor\Keyboard.ico");
            Title = "Keyboard Manager";

            try
            {
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    ExtendsContentIntoTitleBar = true;
                    this.SetTitleBar(titleBar);
                }
            }
            catch (Exception ex) when (ex.HResult == ErrorAlreadyInitialized)
            {
                // Windows App SDK can report this error while initializing a custom title bar on Windows 10.
                Logger.LogError("Failed to customize the title bar; falling back to the default system title bar.", ex);
            }
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
    }
}
