// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class PerformanceOverviewControl : UserControl
{
    private ContentPerformanceOverviewViewModel _viewModel = null!;

    public ContentPerformanceOverviewViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value))
            {
                return;
            }

            _viewModel = value;
            Bindings.Update();
        }
    }

    public PerformanceOverviewControl()
    {
        InitializeComponent();
    }
}
