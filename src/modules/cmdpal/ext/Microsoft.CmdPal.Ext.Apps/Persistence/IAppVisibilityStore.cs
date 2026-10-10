// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Persistence;

/// <summary>Stores raw hidden identities without evaluating catalog visibility.</summary>
internal interface IAppVisibilityStore
{
    /// <summary>Gets an immutable snapshot of the explicitly hidden identities.</summary>
    IReadOnlySet<string> GetSnapshot();

    /// <summary>Replaces the in-memory data and reports whether it changed, without saving it.</summary>
    /// <remarks>The catalog serializes mutations; readers can retain earlier immutable snapshots.</remarks>
    /// <exception cref="System.InvalidOperationException">Existing visibility data could not be read safely at startup.</exception>
    bool SetSnapshot(IReadOnlySet<string> identities);

    /// <summary>Persists the latest in-memory snapshot.</summary>
    /// <remarks>Persistence failures propagate to the caller.</remarks>
    void Persist();
}
