// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public sealed record SourceContext
{
    public PageViewModel Page { get; }

    public AppExtensionHost ExtensionHost { get; }

    public ICommandProviderContext ProviderContext { get; }

    public SourceContext(PageViewModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Page = page;
        ExtensionHost = page.ExtensionHost;
        ProviderContext = page.ProviderContext;
    }
}
