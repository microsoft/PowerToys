// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;

namespace WindowsCommandPalette.Views;

public sealed class DetailsViewModel
{
    internal string Title { get; init; } = string.Empty;

    internal string Body { get; init; } = string.Empty;

    internal IconData HeroImage { get; init; } = new(string.Empty);

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(HeroImage.Icon);

    internal DetailsViewModel(IDetails details)
    {
        this.Title = details.Title;
        this.Body = details.Body;
        this.HeroImage = details.HeroImage?.Dark ?? new(string.Empty);
    }
}
