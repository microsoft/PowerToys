// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public sealed record SourceContext
{
    /// <summary>
    /// Gets the page that sent the command, or null when the sender is not a page
    /// (for example, a dock band that only knows its owning provider).
    /// </summary>
    public PageViewModel? Page { get; }

    public AppExtensionHost ExtensionHost { get; }

    public ICommandProviderContext ProviderContext { get; }

    public SourceContext(PageViewModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Page = page;
        ExtensionHost = page.ExtensionHost;
        ProviderContext = page.ProviderContext;
    }

    public SourceContext(AppExtensionHost extensionHost, ICommandProviderContext providerContext)
    {
        ArgumentNullException.ThrowIfNull(extensionHost);
        ArgumentNullException.ThrowIfNull(providerContext);
        ExtensionHost = extensionHost;
        ProviderContext = providerContext;
    }

    /// <summary>
    /// Creates a source context for a sender that is not a page. Returns null when
    /// the page context does not identify an owning host and provider.
    /// </summary>
    public static SourceContext? FromPageContext(IPageContext? pageContext) =>
        pageContext switch
        {
            PageViewModel page => new(page),
            ICommandContextSource { ExtensionHost: { } host, ProviderContext: { } provider } => new(host, provider),
            _ => null,
        };
}
