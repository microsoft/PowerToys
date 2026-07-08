// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PowerToys.FileLocksmithUI.Helpers;
using WinUIEx;

namespace FileLocksmithUI
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow(bool isElevated)
        {
            InitializeComponent();
            mainPage.ViewModel.IsElevated = isElevated;
            SetTitleBar(titleBar);
            ExtendsContentIntoTitleBar = true;
            AppWindow.SetIcon("Assets/FileLocksmith/Icon.ico");
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());

            var loader = ResourceLoaderInstance.ResourceLoader;
            var title = isElevated ? loader.GetString("AppAdminTitle") : loader.GetString("AppTitle");

            // Guard against an empty title: ResourceLoader.GetString returns "" when the resource
            // map can't be resolved, and an empty native window title can fault the WinUI TitleBar
            // control while it reads AppWindow.Title during a deferred layout pass.
            if (string.IsNullOrEmpty(title))
            {
                title = "File Locksmith";
            }

            Title = title;
            titleBar.Title = title;
        }

        public void Dispose()
        {
        }
    }
}
