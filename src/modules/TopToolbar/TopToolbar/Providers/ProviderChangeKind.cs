// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Providers
{
    /// <summary>
    /// Describes the semantic kind of change a provider is reporting. UI can use this to perform minimal refresh.
    /// Values are intentionally sparse to allow future insertion without breaking binary consumers.
    /// </summary>
    public enum ProviderChangeKind
    {
        Unknown = 0,
        ProviderRegistered = 1, // Provider became available (runtime may create initial group)
        ProviderUnregistered = 2, // Provider removed/unloaded
        GroupAdded = 10,      // A new logical group is now available
        GroupRemoved = 11,    // A previously exposed group should be removed
        GroupUpdated = 12,    // Non-structural updates (name/layout/filter)
        ActionsAdded = 20,    // One or more new actions appended/inserted
        ActionsRemoved = 21,  // One or more actions removed
        ActionsUpdated = 22,  // Metadata or enabled state changed
        ActionExecutionState = 30, // Execution started/stopped/succeeded/failed
        ActionProgress = 31,       // Long running action progress update
        BulkRefresh = 90, // Provider cannot express fine‑grained diff; UI should rebuild provider output
        Reset = 99,       // Hard reset: discard cached state then rediscover fully
    }
}
