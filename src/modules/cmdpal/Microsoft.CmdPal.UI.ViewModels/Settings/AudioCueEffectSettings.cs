// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public sealed record AudioCueEffectSettings
{
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Built-in sound id, <see cref="AudioCueCatalog.CustomSoundId"/>, or null for the default sound.
    /// Kept when the cue is disabled so re-enabling restores the previous choice.
    /// </summary>
    public string? Sound { get; init; }

    /// <summary>Absolute path played when <see cref="Sound"/> is the custom sentinel.</summary>
    public string? CustomSoundPath { get; init; }
}
