// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContentFormControl : UserControl
{
#pragma warning disable CS0649
    private ContentFormViewModel? _viewModel;
#pragma warning restore CS0649

    public ContentFormViewModel? ViewModel { get => _viewModel; set => AttachViewModel(value); }

    static ContentFormControl()
    {
    }

    public ContentFormControl()
    {
    }

    private void AttachViewModel(ContentFormViewModel? vm)
    {
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
    }
}
