// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable

namespace SamplePagesExtension;

/// <summary>Shows the Adaptive Cards charts and visuals that Command Palette renders natively, with live data.</summary>
internal sealed partial class SampleChartsPage : ContentPage, IDisposable
{
    private readonly SampleChartsForm _form = new();

    public SampleChartsPage()
    {
        Name = "Open";
        Title = "Charts and visuals";
        Icon = new IconInfo("\uE9D2"); // AreaChart
    }

    public override IContent[] GetContent()
    {
        _form.Start();
        return [_form];
    }

    public void Dispose() => _form.Dispose();
}
