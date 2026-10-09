// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.ModuleServices;

/// <summary>
/// Represents the current state of the Awake module.
/// </summary>
/// <param name="IsRunning">Whether the Awake process is currently running.</param>
/// <param name="Mode">The current Awake mode (Passive, Indefinite, Timed, Expirable).</param>
/// <param name="KeepDisplayOn">Whether the display is kept on.</param>
/// <param name="Duration">For timed mode, the configured duration.</param>
/// <param name="Expiration">For expirable mode, the expiration date/time.</param>
public readonly record struct AwakeState(bool IsRunning, AwakeStateMode Mode, bool KeepDisplayOn, TimeSpan? Duration, DateTimeOffset? Expiration);

/// <summary>
/// The mode of the Awake module.
/// </summary>
public enum AwakeStateMode
{
    Passive = 0,
    Indefinite = 1,
    Timed = 2,
    Expirable = 3,
}
