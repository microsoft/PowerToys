// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

// Advance only after each milestone succeeds. An interrupted earlier phase
// deliberately asks for more recovery, never for less than the page needs.
internal enum ListPageFetchPhase
{
    // Includes requests deferred while suspended, before a worker claims them.
    Fetching,

    // Items owns the snapshot, but FilteredItems/ItemsUpdated have not caught up.
    Committed,

    // Back needs only to restart unfinished initialization of retained rows.
    Published,
}
