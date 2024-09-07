// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace WindowsCommandPalette.Views;

public sealed partial class DetailsControl : UserControl
{
    public DetailsViewModel ViewModel { get; set; }

    public DetailsControl(DetailsViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }
}
