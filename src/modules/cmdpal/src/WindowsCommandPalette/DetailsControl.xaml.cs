// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Windows.CommandPalette.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DeveloperCommandPalette;

public sealed class DetailsViewModel
{
    internal string Title { get; init; } = "";
    internal string Body { get; init; } = "";
    internal IconDataType HeroImage { get; init; } = new("");
    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(HeroImage.Icon);

    internal DetailsViewModel(IDetails details)
    {
        this.Title = details.Title;
        this.Body = details.Body;
        this.HeroImage = details.HeroImage ?? new("");
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
