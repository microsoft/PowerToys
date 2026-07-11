// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Describes a single audio cue: its settings key / sound file-name stem and UI metadata.
/// </summary>
public sealed record AudioCueDefinition(AudioCue Cue, string Id, string DisplayNameResourceKey, int ThrottleMilliseconds = 0)
{
    /// <summary>System sounds from <see cref="AudioCueCatalog.SystemSounds"/> offered for this cue.</summary>
    public IReadOnlyList<string> SystemSoundIds { get; init; } = [];

    /// <summary>Whether the cue plays before the user has configured it; chatty cues ship opt-in.</summary>
    public bool EnabledByDefault { get; init; } = true;
}
