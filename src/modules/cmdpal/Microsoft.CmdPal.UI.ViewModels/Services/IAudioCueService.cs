// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Starts short, non-blocking audio feedback for Command Palette interactions.
/// </summary>
public interface IAudioCueService
{
    /// <summary>
    /// Plays a cue when global and per-effect audio settings allow it.
    /// </summary>
    void Play(AudioCue cue);

    /// <summary>
    /// Previews a cue at the configured volume, regardless of its enabled state.
    /// </summary>
    void Preview(AudioCue cue);

    /// <summary>
    /// Auditions a specific sound for a cue without persisting anything, e.g. while the user
    /// browses the sound picker. <paramref name="customSoundPath"/> only matters for the
    /// custom sound id.
    /// </summary>
    void Preview(AudioCue cue, string? soundId, string? customSoundPath = null);
}
