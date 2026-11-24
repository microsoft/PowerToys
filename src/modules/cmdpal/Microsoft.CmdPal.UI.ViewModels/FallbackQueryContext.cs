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

    /// <summary>
    /// Takes a reference on the snapshot, or returns null when there is nothing to hold.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="TryAcquireSnapshotLease"/>. Use this only where a closed
    /// snapshot needs no special handling, because the caller drops the result anyway.
    /// </remarks>
    public IDisposable? AcquireSnapshotLease() => SnapshotLease?.Acquire();

    /// <summary>
    /// Takes a reference on the snapshot that produced this result, if there is one.
    /// </summary>
    /// <param name="lease">
    /// Receives the new reference. It is null when this context has no snapshot,
    /// which is the usual case for a fallback that supplies only a command.
    /// Release it when the operation ends.
    /// </param>
    /// <returns>
    /// False when the snapshot closed before the caller asked for it. The caller must
    /// then abandon the operation: the extension objects behind the snapshot are gone.
    /// </returns>
    public bool TryAcquireSnapshotLease(out IDisposable? lease)
    {
        lease = SnapshotLease?.Acquire();
        return lease is not null || SnapshotLease is null;
    }

    internal FallbackQueryContext WithInvocationContext(object invocationContext)
        => this with { InvocationContext = invocationContext };
}
