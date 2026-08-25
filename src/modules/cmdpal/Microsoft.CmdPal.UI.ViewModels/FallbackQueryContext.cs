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
        : this(extensionHost, providerContext, invocationContext, null, queryToken)
    {
    }

    internal FallbackQueryContext(
        AppExtensionHost extensionHost,
        ICommandProviderContext providerContext,
        object invocationContext,
        FallbackSnapshotLease? snapshotLease,
        CancellationToken queryToken)
    {
        ExtensionHost = extensionHost;
        ProviderContext = providerContext;
        InvocationContext = invocationContext;
        QueryToken = queryToken;
        SnapshotLease = snapshotLease;
    }

    public AppExtensionHost ExtensionHost { get; init; }

    public ICommandProviderContext ProviderContext { get; init; }

    public object InvocationContext { get; init; }

    public CancellationToken QueryToken { get; init; }

    internal FallbackSnapshotLease? SnapshotLease { get; init; }

    public bool CanInvoke => !QueryToken.IsCancellationRequested;

    public IDisposable? AcquireSnapshotLease() => SnapshotLease?.Acquire();

    internal bool HasSnapshotLease => SnapshotLease is not null;

    internal FallbackQueryContext WithInvocationContext(object invocationContext)
        => this with { InvocationContext = invocationContext };
}
