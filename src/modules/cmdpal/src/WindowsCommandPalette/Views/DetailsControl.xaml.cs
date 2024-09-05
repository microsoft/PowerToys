// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace WindowsCommandPalette.Views;

public sealed partial class DetailsControl : UserControl
{
    private readonly DetailsViewModel ViewModel;

    public DetailsControl(DetailsViewModel vm)
    {
        this.ViewModel = vm;
        this.InitializeComponent();
    }
}
