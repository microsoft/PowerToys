// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Panels
{
    public sealed partial class MouseJumpPanel : UserControl
    {
        internal MouseUtilsViewModel ViewModel { get; set; }

        public MouseJumpPanel()
        {
            InitializeComponent();
        }
    }
}
