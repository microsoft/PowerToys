// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariables.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace EnvironmentVariables
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);

            AppWindow.SetIcon("Assets/EnvironmentVariables/EnvironmentVariables.ico");
            Title = ResourceLoaderInstance.ResourceLoader.GetString("WindowTitle");
        }
    }
}
