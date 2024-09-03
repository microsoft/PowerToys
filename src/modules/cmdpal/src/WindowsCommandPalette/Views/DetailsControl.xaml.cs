// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.CommandPalette.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace WindowsCommandPalette.Views;

public sealed class DetailsViewModel
{
    internal string Title { get; init; } = string.Empty;

    internal string Body { get; init; } = string.Empty;

    internal IconDataType HeroImage { get; init; } = new(string.Empty);

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(HeroImage.Icon);

    internal DetailsViewModel(IDetails details)
    {
        this.Title = details.Title;
        this.Body = details.Body;
        this.HeroImage = details.HeroImage ?? new(string.Empty);
    }
}

public sealed partial class DetailsControl : UserControl
{
    private readonly DetailsViewModel ViewModel;

    public DetailsControl(DetailsViewModel vm)
    {
        this.ViewModel = vm;
        this.InitializeComponent();
    }
}
