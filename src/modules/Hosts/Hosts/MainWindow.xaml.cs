// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Hosts
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = "Hosts Editor";
            SetTitleBar();
        }

        private void SetTitleBar()
        {
            AppWindow window = this.GetAppWindow();
            window.TitleBar.ExtendsContentIntoTitleBar = true;
            window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            this.SetTitleBar(titleBar);
        }
    }
}
