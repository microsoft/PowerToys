// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// A sample dock band with multiple buttons.
/// Each button shows a toast message when clicked.
/// </summary>
internal sealed partial class SampleButtonsDockBand : WrappedDockItem
{
    public SampleButtonsDockBand()
        : base([], "com.microsoft.cmdpal.samples.buttons_band", "Sample Buttons Band")
    {
        ListItem[] buttons = [
            new(new ShowToastCommand("Button 1")) { Title = "1" },
            new(new ShowToastCommand("Button B")) { Icon = new IconInfo("\uF094") }, // B button
            new(new ShowToastCommand("Button 3")) { Title = "Items have Icons &", Icon = new IconInfo("\uED1E"), Subtitle = "titles & subtitles" }, // Subtitles
        ];
        Icon = new IconInfo("\uEECA"); // ButtonView2
        Items = buttons;
    }
}

#pragma warning restore SA1402 // File may only contain a single type
