// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleActiveFallback : FallbackCommandItem3
{
    private string _query = string.Empty;

    internal SampleActiveFallback()
        : base("Sample active fallback", "com.microsoft.cmdpal.sample.fallback.active")
    {
        Name = "Run active sample";
        Title = "Run active sample";
        Subtitle = "The extension updates this row for each query.";
        Icon = new IconInfo("\uE945");
    }

    public override void UpdateQuery(string query)
    {
        _query = query;
        Title = $"Active sample: {query}";
    }

    public override ICommand CreateCommand(IFallbackCommandInvocationArgs args)
    {
        return new ShowToastCommand(_query)
        {
            Id = "com.microsoft.cmdpal.sample.fallback.active.invoke",
            Name = Name,
        };
    }
}
