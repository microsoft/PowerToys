// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SamplePassiveFallback : PassiveFallbackCommandItem
{
    internal SamplePassiveFallback()
        : base("Sample web search", "com.microsoft.cmdpal.sample.fallback.passive")
    {
        Name = "Search the web";
        Title = "Search the web";
        TitleTemplate = "Search the web for \"{query}\"";
        SubtitleTemplate = "Search the web in the default browser";
        MatchKind = HostMatchKind.Regex;
        MatchValue = @".+";
        Icon = new IconInfo("\uE721");
    }

    public override ICommand CreateCommand(IFallbackCommandInvocationArgs args)
    {
        return new OpenUrlCommand($"https://www.bing.com/search?q={Uri.EscapeDataString(args.Query)}")
        {
            Name = Name,
            Id = "com.microsoft.cmdpal.sample.fallback.passive.invoke",
        };
    }
}
