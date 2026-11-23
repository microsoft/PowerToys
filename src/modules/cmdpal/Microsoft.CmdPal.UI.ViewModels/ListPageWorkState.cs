// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Atomically owns the current visit/fetch and its remaining work. Navigation
/// invalidates the generation while retaining the phase and publication intent,
/// so even a fetch still blocked in extension code can be recovered on Back.
/// </summary>
internal sealed record ListPageWorkState(
    int Generation,
    ListPageWorkStatus Status,
    ListPageFetchPhase Phase,
    bool KeepSelection = true,
    bool EnsureSelectionVisible = false);
