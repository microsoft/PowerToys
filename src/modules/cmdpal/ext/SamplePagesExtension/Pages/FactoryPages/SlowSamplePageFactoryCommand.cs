// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

public sealed partial class SlowSamplePageFactoryCommand : PageFactoryCommand
{
    private readonly SamplePageFactoryPage _items;

    public SlowSamplePageFactoryCommand(SamplePageFactoryPage items)
    {
        _items = items;
        Icon = new IconInfo("\uF157");
    }

    public async override Task<IPage> CreatePageAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        var newPage = new SampleTimeCapturePage(Guid.CreateVersion7(DateTimeOffset.UtcNow).ToString());
        _items.AddPage(newPage);
        return newPage;
    }
}
