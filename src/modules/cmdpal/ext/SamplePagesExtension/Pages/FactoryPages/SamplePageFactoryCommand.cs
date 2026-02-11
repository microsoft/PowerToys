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

public sealed partial class SamplePageFactoryCommand : PageFactoryCommand
{
    private readonly SamplePageFactoryPage _items;

    public SamplePageFactoryCommand(SamplePageFactoryPage items)
    {
        _items = items;
        Icon = new IconInfo("\uF158");
    }

    public override Task<IPage> CreatePageAsync(CancellationToken cancellationToken)
    {
        var newPage = new SampleTimeCapturePage(Guid.CreateVersion7(DateTimeOffset.UtcNow).ToString());
        _items.AddPage(newPage);
        return Task.FromResult<IPage>(newPage);
    }
}
