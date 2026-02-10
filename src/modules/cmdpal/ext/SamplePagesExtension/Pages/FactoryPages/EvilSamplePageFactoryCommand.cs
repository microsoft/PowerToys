// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

public sealed partial class EvilSamplePageFactoryCommand : PageFactoryCommand
{
    private readonly SamplePageFactoryPage _items;

    public EvilSamplePageFactoryCommand(SamplePageFactoryPage items)
    {
        _items = items;
        Icon = new IconInfo("\uF159");
    }

    public override IPage CreatePage()
    {
        Thread.Sleep(4096);
        var newPage = new SampleTimeCapturePage(Guid.CreateVersion7(DateTimeOffset.UtcNow).ToString());
        _items.AddPage(newPage);
        return newPage;
    }
}
