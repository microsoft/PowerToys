// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariables.Helpers;
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
            var loader = ResourceLoaderInstance.ResourceLoader;
            var title = App.GetService<IElevationHelper>().IsElevated ? loader.GetString("WindowAdminTitle") : loader.GetString("WindowTitle");
            Title = title;
            AppTitleTextBlock.Text = title;
        }
    }
}
