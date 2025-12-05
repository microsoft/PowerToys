// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CommandPalette.UI.Pages;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel viewModel;

    public ShellPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
    }
}
