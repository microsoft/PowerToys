// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace TopToolbar.Stores
{
    /// <summary>
    /// Describes a change emitted by <see cref="ToolbarStore"/>.
    /// </summary>
    public sealed class StoreChangedEventArgs : EventArgs
    {
        public StoreChangeKind Kind { get; }

        /// <summary>
        /// Gets the group id impacted (for upsert / remove). Null when a full reset occurred.
        /// </summary>
        public string GroupId { get; }

        private StoreChangedEventArgs(StoreChangeKind kind, string groupId)
        {
            Kind = kind;
            GroupId = groupId;
        }

        public static StoreChangedEventArgs Upsert(string id) => new StoreChangedEventArgs(StoreChangeKind.GroupUpserted, id);

        public static StoreChangedEventArgs Removed(string id) => new StoreChangedEventArgs(StoreChangeKind.GroupRemoved, id);

        public static StoreChangedEventArgs Reset() => new StoreChangedEventArgs(StoreChangeKind.Reset, null);
    }

    public enum StoreChangeKind
    {
        GroupUpserted,
        GroupRemoved,
        Reset,
    }
}
