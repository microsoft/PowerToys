// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using ManagedCommon;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using WinUIEx;

namespace KeyboardManagerEditorUI
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            this.InitializeComponent();
            SetTitleBar();
        }

        private void SetTitleBar()
        {
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
            ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);
            this.SetIcon("Assets\\KeyboardManagerEditor\\icon.ico");
            Title = ResourceLoaderInstance.ResourceLoader.GetString("WindowTitle");
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.SetIcon("Assets\\KeyboardManagerEditor\\icon.ico");
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
