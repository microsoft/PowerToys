// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SpongebotExtension;

internal sealed class SpongebotSettingsPage : FormPage
{
    private readonly SpongeSettingsForm settingsForm = new();

    public override IForm[] Forms() => [settingsForm];

    public SpongebotSettingsPage()
    {
        Name = "Settings";
        Icon = new("https://imgflip.com/s/meme/Mocking-Spongebob.jpg");
    }
}
