// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed record FallbackQueryContext
{
    public FallbackQueryContext(
        AppExtensionHost extensionHost,
        ICommandProviderContext providerContext,
        object invocationContext,
        CancellationToken queryToken)
    {
        ExtensionHost = extensionHost;
        ProviderContext = providerContext;
        InvocationContext = invocationContext;
        QueryToken = queryToken;
    }

    public AppExtensionHost ExtensionHost { get; init; }

    public ICommandProviderContext ProviderContext { get; init; }

    public object InvocationContext { get; init; }

    public CancellationToken QueryToken { get; init; }

    public bool CanInvoke => !QueryToken.IsCancellationRequested;
}
