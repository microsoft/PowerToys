// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleActiveFallback : FallbackCommandItem
{
    internal SampleActiveFallback()
        : base("Sample active fallback", "com.microsoft.cmdpal.sample.fallback.active")
    {
        Title = "Run active sample";
        Subtitle = "The extension updates this row for each query.";
        Icon = new IconInfo("\uE945");
    }

    public override void UpdateQuery(string query)
    {
        Title = $"Active sample: {query}";
        Command = new ShowToastCommand(query)
        {
            Id = "com.microsoft.cmdpal.sample.fallback.active.invoke",
            Name = "Run active sample",
        };
    }
}
