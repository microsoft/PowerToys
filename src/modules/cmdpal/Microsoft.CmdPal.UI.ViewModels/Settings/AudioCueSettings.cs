// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public sealed record AudioCueSettings
{
    private Dictionary<string, AudioCueEffectSettings>? _effects;

    public bool IsEnabled { get; init; }

    public int Volume { get; init; } = 50;

    /// <summary>
    /// Per-cue settings keyed by <see cref="AudioCueDefinition.Id"/>. Missing entries mean defaults.
    /// </summary>
    public Dictionary<string, AudioCueEffectSettings> Effects
    {
        get => _effects ??= new(StringComparer.OrdinalIgnoreCase);
        init => _effects = value;
    }

    public AudioCueEffectSettings GetEffect(AudioCue cue)
    {
        var definition = AudioCueCatalog.GetDefinition(cue);
        return _effects?.GetValueOrDefault(definition.Id) ?? new() { IsEnabled = definition.EnabledByDefault };
    }

    public AudioCueSettings WithEffect(AudioCue cue, AudioCueEffectSettings effect)
    {
        var effects = new Dictionary<string, AudioCueEffectSettings>(Effects, StringComparer.OrdinalIgnoreCase)
        {
            [AudioCueCatalog.GetDefinition(cue).Id] = effect,
        };

        return this with { Effects = effects };
    }
}
