// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Helpers;

/// <summary>
/// Pure decision logic for console-display power-state transitions. Extracted
/// from <c>DisplayChangeWatcher</c> so it can be unit-tested in isolation from
/// the P/Invoke surface and dispatcher plumbing.
/// </summary>
public static class DisplayStateTransition
{
    /// <summary>
    /// Returns true when the watcher should treat this transition as a wake
    /// event and notify subscribers. Wake = transition INTO the ON state from
    /// any other state (off / dimmed / unknown). Other transitions are not
    /// actionable: ON→ON is a subscription echo, ON→OFF / ON→DIMMED are the
    /// user blanking the display (no rediscovery needed yet).
    /// </summary>
    public static bool ShouldTriggerOn(uint newState, uint lastState)
        => newState == PowerSettingsNative.DisplayStateOn
           && lastState != PowerSettingsNative.DisplayStateOn;
}
