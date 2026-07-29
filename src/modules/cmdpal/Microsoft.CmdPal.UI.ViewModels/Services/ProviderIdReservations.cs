// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// A synchronized registry of provider ids (normalized manifest name keys) that are
/// currently claimed by a loaded extension, keyed to the canonical directory that owns
/// them. Every registration path (initial scan, refresh, dynamic install, hot-reload,
/// and crash-restart) consults and claims the same registry as one atomic step, so two
/// extensions can never register the same provider id regardless of how they arrive.
/// </summary>
/// <remarks>
/// The registry keeps its own lock so it is independently thread-safe. The service also
/// invokes it while holding its extensions lock so that the id claim and the in-memory
/// extension list stay consistent as a single atomic operation.
/// </remarks>
internal sealed class ProviderIdReservations
{
    private readonly Lock _lock = new();

    // Provider id (ordinal name key) -> canonical directory that owns it.
    private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);

    /// <summary>
    /// Atomically claims <paramref name="providerId"/> for <paramref name="canonicalDirectory"/>.
    /// An empty provider id is never reserved (there is nothing to collide on).
    /// </summary>
    /// <param name="providerId">The normalized provider id (manifest name key).</param>
    /// <param name="canonicalDirectory">The canonical directory attempting to own the id.</param>
    /// <returns>
    /// True when the id is now owned by <paramref name="canonicalDirectory"/> (either newly
    /// claimed or already owned by the same directory); false when a different directory
    /// already owns it.
    /// </returns>
    public bool TryReserve(string? providerId, string canonicalDirectory)
    {
        if (string.IsNullOrEmpty(providerId))
        {
            return true;
        }

        lock (_lock)
        {
            if (_owners.TryGetValue(providerId, out var owner))
            {
                return string.Equals(owner, canonicalDirectory, StringComparison.OrdinalIgnoreCase);
            }

            _owners[providerId] = canonicalDirectory;
            return true;
        }
    }

    /// <summary>
    /// Releases <paramref name="providerId"/> only when it is currently owned by
    /// <paramref name="canonicalDirectory"/>, so a stale release from a different owner
    /// cannot free an id that has since been claimed by someone else.
    /// </summary>
    /// <param name="providerId">The normalized provider id (manifest name key).</param>
    /// <param name="canonicalDirectory">The canonical directory releasing the id.</param>
    public void Release(string? providerId, string canonicalDirectory)
    {
        if (string.IsNullOrEmpty(providerId))
        {
            return;
        }

        lock (_lock)
        {
            if (_owners.TryGetValue(providerId, out var owner) &&
                string.Equals(owner, canonicalDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _owners.Remove(providerId);
            }
        }
    }

    /// <summary>
    /// Drops every reservation. Used when the service stops or is disposed and all
    /// extensions are torn down together.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _owners.Clear();
        }
    }
}
