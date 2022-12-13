// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace FileLocksmithUI
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow(bool isElevated)
        {
            InitializeComponent();
            mainPage.ViewModel.IsElevated = isElevated;
            SetTitleBar();
        }

        private void SetTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
        }

        public void Dispose()
        {
        }
    }
}
