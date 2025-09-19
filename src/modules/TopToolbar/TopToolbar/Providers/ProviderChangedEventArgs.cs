// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace TopToolbar.Providers
{
    /// <summary>
    /// Event payload describing a provider data change.
    /// Provider implementations should prefer factory helpers (e.g. <see cref="ActionsUpdated"/>)
    /// for common scenarios; custom payloads can be supplied via <see cref="Payload"/>.
    /// </summary>
    public sealed class ProviderChangedEventArgs : EventArgs
    {
        /// <summary>Gets the stable provider identifier emitting the change.</summary>
        public string ProviderId { get; }

        /// <summary>Gets the semantic change category.</summary>
        public ProviderChangeKind Kind { get; }

        /// <summary>Gets the groups impacted (null = not specified, empty = known none).</summary>
        public IReadOnlyList<string> AffectedGroupIds { get; }

        /// <summary>Gets the actions impacted (null = not specified, empty = known none).</summary>
        public IReadOnlyList<string> AffectedActionIds { get; }

        /// <summary>Gets the monotonic version assigned by runtime when dispatching.</summary>
        public long Version { get; internal set; }

        /// <summary>Gets the UTC timestamp of creation (provider side).</summary>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        /// <summary>Gets the optional strongly typed payload (execution state, progress, etc.).</summary>
        public object Payload { get; }

        /// <summary>Gets the optional lightweight metadata bag (string/object pairs) for extensions.</summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        public ProviderChangedEventArgs(
            string providerId,
            ProviderChangeKind kind,
            IReadOnlyList<string> affectedGroupIds = null,
            IReadOnlyList<string> affectedActionIds = null,
            object payload = null,
            IReadOnlyDictionary<string, object> metadata = null)
        {
            ProviderId = providerId ?? string.Empty;
            Kind = kind;
            AffectedGroupIds = affectedGroupIds;
            AffectedActionIds = affectedActionIds;
            Payload = payload;
            Metadata = metadata;
        }

        public static ProviderChangedEventArgs Bulk(string providerId) => new(providerId, ProviderChangeKind.BulkRefresh);

        public static ProviderChangedEventArgs ResetAll(string providerId) => new(providerId, ProviderChangeKind.Reset);

        public static ProviderChangedEventArgs ActionsUpdated(string providerId, IReadOnlyList<string> actionIds) => new(providerId, ProviderChangeKind.ActionsUpdated, affectedActionIds: actionIds);

        public static ProviderChangedEventArgs ActionsAdded(string providerId, IReadOnlyList<string> actionIds) => new(providerId, ProviderChangeKind.ActionsAdded, affectedActionIds: actionIds);

        public static ProviderChangedEventArgs ActionsRemoved(string providerId, IReadOnlyList<string> actionIds) => new(providerId, ProviderChangeKind.ActionsRemoved, affectedActionIds: actionIds);

        public static ProviderChangedEventArgs GroupUpdated(string providerId, IReadOnlyList<string> groupIds) => new(providerId, ProviderChangeKind.GroupUpdated, affectedGroupIds: groupIds);
    }
}
