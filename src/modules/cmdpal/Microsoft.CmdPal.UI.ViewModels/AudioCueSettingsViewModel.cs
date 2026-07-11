// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class AudioCueSettingsViewModel
{
    private static readonly CompositeFormat _soundSelectorNameFormat = CompositeFormat.Parse(GetResource("audio_cue_sound_selector_name"));

    private readonly ISettingsService _settingsService;

    public IReadOnlyList<AudioCueEffectSettingsViewModel> Effects { get; }

    /// <summary>Rows for the settings expander: the volume card (this) followed by one card per cue.</summary>
    public IReadOnlyList<object> ExpanderItems { get; }

    public bool IsEnabled
    {
        get => _settingsService.Settings.AudioCues.IsEnabled;
        set => _settingsService.UpdateSettings(s => s with { AudioCues = s.AudioCues with { IsEnabled = value } }, hotReload: false);
    }

    public double Volume
    {
        get => _settingsService.Settings.AudioCues.Volume;
        set
        {
            var volume = Math.Clamp((int)Math.Round(value), 0, 100);
            _settingsService.UpdateSettings(s => s with { AudioCues = s.AudioCues with { Volume = volume } }, hotReload: false);
        }
    }

    public AudioCueSettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Effects = AudioCueCatalog.Cues.Select(CreateEffect).ToArray();
        ExpanderItems = [this, .. Effects];
    }

    internal AudioCueEffectSettings GetEffectSettings(AudioCue cue) => _settingsService.Settings.AudioCues.GetEffect(cue);

    internal void UpdateEffect(AudioCue cue, Func<AudioCueEffectSettings, AudioCueEffectSettings> transform)
    {
        _settingsService.UpdateSettings(
            s =>
            {
                var audioCues = s.AudioCues;
                return s with { AudioCues = audioCues.WithEffect(cue, transform(audioCues.GetEffect(cue))) };
            },
            hotReload: false);
    }

    private AudioCueEffectSettingsViewModel CreateEffect(AudioCueDefinition definition)
    {
        var displayName = GetResource(definition.DisplayNameResourceKey);
        return new(
            this,
            definition.Cue,
            displayName,
            string.Format(CultureInfo.CurrentCulture, _soundSelectorNameFormat, displayName),
            BuildSoundOptions(definition));
    }

    private static AudioCueSoundOption[] BuildSoundOptions(AudioCueDefinition definition) =>
    [
        new(null, GetResource("audio_cue_sound_none")),
        .. AudioCueCatalog.BuiltInSounds.Select(sound => new AudioCueSoundOption(sound.Id, GetResource(sound.DisplayNameResourceKey))),
        .. definition.SystemSoundIds
            .Select(id => AudioCueCatalog.TryGetSystemSound(id, out var sound) ? new AudioCueSoundOption(sound.Id, GetResource(sound.DisplayNameResourceKey)) : null)
            .OfType<AudioCueSoundOption>(),
        new(AudioCueCatalog.CustomSoundId, GetResource("audio_cue_sound_custom")),
    ];

    private static string GetResource(string key) => Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? key;
}
