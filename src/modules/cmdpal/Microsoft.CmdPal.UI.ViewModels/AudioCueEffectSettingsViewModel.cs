// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class AudioCueEffectSettingsViewModel : ObservableObject
{
    private readonly AudioCueSettingsViewModel _owner;

    public AudioCue Cue { get; }

    public string DisplayName { get; }

    public string SoundSelectorName { get; }

    public string SoundSelectorAutomationId => $"CmdPal_AudioCues_{Cue}Sound";

    public string PreviewAutomationId => $"CmdPal_AudioCues_{Cue}Preview";

    public string BrowseAutomationId => $"CmdPal_AudioCues_{Cue}Browse";

    public IReadOnlyList<AudioCueSoundOption> SoundOptions { get; }

    public AudioCueSoundOption SelectedSound
    {
        get
        {
            var effect = _owner.GetEffectSettings(Cue);
            var soundId = effect.IsEnabled ? AudioCueCatalog.ResolveSoundId(effect.Sound) : null;

            // A resolved id can still be missing here (e.g. a system sound no longer offered
            // for this cue); show the default sound rather than pretending the cue is off.
            return SoundOptions.FirstOrDefault(option => option.SoundId == soundId)
                ?? SoundOptions.FirstOrDefault(option => option.SoundId == AudioCueCatalog.DefaultSoundId)
                ?? SoundOptions[0];
        }

        set
        {
            if (value is null || SelectedSound.SoundId == value.SoundId)
            {
                return;
            }

            // "None" only disables the cue; the last chosen sound survives re-enabling.
            _owner.UpdateEffect(Cue, effect => value.SoundId is null
                ? effect with { IsEnabled = false }
                : effect with { IsEnabled = true, Sound = value.SoundId });
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomSelected));
            OnPropertyChanged(nameof(CustomSoundDescription));
        }
    }

    public bool IsCustomSelected => SelectedSound.SoundId == AudioCueCatalog.CustomSoundId;

    public string? CustomSoundPath => _owner.GetEffectSettings(Cue).CustomSoundPath;

    /// <summary>Shown as the settings card description; only meaningful while Custom is selected.</summary>
    public string? CustomSoundDescription => IsCustomSelected ? CustomSoundPath : null;

    internal AudioCueEffectSettingsViewModel(
        AudioCueSettingsViewModel owner,
        AudioCue cue,
        string displayName,
        string soundSelectorName,
        IReadOnlyList<AudioCueSoundOption> soundOptions)
    {
        _owner = owner;
        Cue = cue;
        DisplayName = displayName;
        SoundSelectorName = soundSelectorName;
        SoundOptions = soundOptions;
    }

    public void SetCustomSoundPath(string path)
    {
        _owner.UpdateEffect(Cue, effect => effect with
        {
            IsEnabled = true,
            Sound = AudioCueCatalog.CustomSoundId,
            CustomSoundPath = path,
        });
        OnPropertyChanged(nameof(SelectedSound));
        OnPropertyChanged(nameof(IsCustomSelected));
        OnPropertyChanged(nameof(CustomSoundPath));
        OnPropertyChanged(nameof(CustomSoundDescription));
    }
}
