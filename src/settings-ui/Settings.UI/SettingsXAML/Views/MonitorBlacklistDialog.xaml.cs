// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// DIAGNOSTIC stub. Stripped from the full two-mode (list / form) implementation to bisect
    /// the persistent XamlParseException (HResult 0x80073B0A = ERROR_MRM_INVALID_QUALIFIER).
    /// If this minimal dialog opens cleanly the bug lives in the previous content
    /// (likely an x:Uid / resw key); if it still crashes, the bug is in the wrapper or page
    /// integration. The full implementation will be restored once the offending element is found.
    /// </summary>
    public sealed partial class MonitorBlacklistDialog : ContentDialog
    {
        public PowerDisplayViewModel ViewModel { get; }

        public MonitorBlacklistDialog(PowerDisplayViewModel viewModel)
        {
            ViewModel = viewModel;
            this.InitializeComponent();
            Title = "Monitor blacklist (diagnostic)";
            CloseButtonText = "Close";
        }
    }
}
