// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Single registration point for audio cues and built-in sounds. Adding a cue or sound here
/// (plus its wav assets and display-name resources) is all the settings UI and playback need.
/// </summary>
public static class AudioCueCatalog
{
    /// <summary>Sentinel sound id meaning "play the file at <see cref="AudioCueEffectSettings.CustomSoundPath"/>".</summary>
    public const string CustomSoundId = "custom";

    /// <summary>Built-in sound used when settings carry no (or an unknown) sound id.</summary>
    public const string DefaultSoundId = "soft";

    public static IReadOnlyList<AudioCueDefinition> Cues { get; } =
    [
        new(AudioCue.OpenPalette, "open-palette", "audio_cue_event_open_palette"),
        new(AudioCue.HidePalette, "hide-palette", "audio_cue_event_hide_palette"),
        new(AudioCue.PageTransitionForward, "page-forward", "audio_cue_event_page_transition_forward")
        {
            SystemSoundIds = ["system-navigation"],
        },
        new(AudioCue.PageTransitionBack, "page-back", "audio_cue_event_page_transition_back")
        {
            SystemSoundIds = ["system-navigation"],
        },
        new(AudioCue.ActionExecution, "action-execution", "audio_cue_event_action_execution")
        {
            SystemSoundIds = ["system-navigation", "system-ding"],
        },
        new(AudioCue.SelectionChange, "selection-change", "audio_cue_event_selection_change", ThrottleMilliseconds: 40),
        new(AudioCue.FocusChange, "focus-change", "audio_cue_event_focus_change", ThrottleMilliseconds: 100)
        {
            EnabledByDefault = false,
        },
        new(AudioCue.ConfirmationPopup, "confirmation-popup", "audio_cue_event_confirmation_popup")
        {
            SystemSoundIds = ["system-asterisk", "system-exclamation"],
        },
        new(AudioCue.ToastShown, "toast-shown", "audio_cue_event_toast_shown")
        {
            SystemSoundIds = ["system-notification", "system-asterisk", "system-exclamation"],
        },
        new(AudioCue.StatusMessage, "status-message", "audio_cue_event_status_message")
        {
            SystemSoundIds = ["system-asterisk", "system-ding", "system-notification"],
        },
    ];

    public static IReadOnlyList<AudioCueSoundDefinition> BuiltInSounds { get; } =
    [
        new("soft", "audio_cue_sound_soft"),
        new("warm", "audio_cue_sound_warm"),
        new("calm", "audio_cue_sound_calm"),
        new("glass", "audio_cue_sound_glass"),
        new("breeze", "audio_cue_sound_breeze"),
        new("retro", "audio_cue_sound_retro"),
        new("electric", "audio_cue_sound_electric"),
    ];

    public static IReadOnlyList<AudioCueSystemSound> SystemSounds { get; } =
    [
        new("system-asterisk", "audio_cue_sound_system_asterisk", ".Default", "SystemAsterisk", "Windows Background.wav"),
        new("system-exclamation", "audio_cue_sound_system_exclamation", ".Default", "SystemExclamation", "Windows Foreground.wav"),
        new("system-notification", "audio_cue_sound_system_notification", ".Default", "Notification.Default", "Windows Notify System Generic.wav"),
        new("system-ding", "audio_cue_sound_system_ding", null, null, "Windows Ding.wav"),
        new("system-navigation", "audio_cue_sound_system_navigation", "Explorer", "Navigating", "Windows Navigation Start.wav"),
    ];

    private static readonly Dictionary<AudioCue, AudioCueDefinition> _definitionsByCue = Cues.ToDictionary(definition => definition.Cue);

    private static readonly HashSet<string> _builtInSoundIds = BuiltInSounds.Select(sound => sound.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, AudioCueSystemSound> _systemSoundsById = SystemSounds.ToDictionary(sound => sound.Id, StringComparer.OrdinalIgnoreCase);

    public static AudioCueDefinition GetDefinition(AudioCue cue) =>
        _definitionsByCue.TryGetValue(cue, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(cue));

    public static bool TryGetSystemSound(string? soundId, out AudioCueSystemSound sound)
    {
        if (soundId is not null && _systemSoundsById.TryGetValue(soundId, out var found))
        {
            sound = found;
            return true;
        }

        sound = null!;
        return false;
    }

    /// <summary>
    /// Maps a persisted sound id to something playable: a built-in id, a system sound id, or
    /// <see cref="CustomSoundId"/>. Unknown or missing ids fall back to the default sound.
    /// </summary>
    public static string ResolveSoundId(string? soundId)
    {
        if (string.Equals(soundId, CustomSoundId, StringComparison.OrdinalIgnoreCase))
        {
            return CustomSoundId;
        }

        if (soundId is not null)
        {
            if (TryGetSystemSound(soundId, out var systemSound))
            {
                return systemSound.Id;
            }

            if (_builtInSoundIds.Contains(soundId))
            {
                return soundId;
            }
        }

        return DefaultSoundId;
    }
}
