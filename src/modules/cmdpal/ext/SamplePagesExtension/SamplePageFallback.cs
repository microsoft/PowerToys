// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SamplePageFallback : PassiveFallbackCommandItem
{
    internal SamplePageFallback()
        : base("Sample query page", "com.microsoft.cmdpal.sample.fallback.page")
    {
        Name = "Open sample query page";
        Title = "Open sample query page";
        TitleTemplate = "Open a sample page for \"{query}\"";
        SubtitleTemplate = "Open a sample page that contains the query";
        Icon = new IconInfo("\uE8A5");
    }

    public override ICommand CreateCommand(IFallbackCommandInvocationArgs args) => new SampleQueryPage(args.Query);
}
