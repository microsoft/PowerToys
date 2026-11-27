// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Outcome of saving the add/edit remapping dialog.</summary>
    internal enum MappingSaveResult
    {
        Saved,
        Failed,

        /// <summary>
        /// Another profile became active after the dialog opened. Nothing was kept in the newly
        /// active profile; the dialog tells the user which profile it was opened for.
        /// </summary>
        ProfileChanged,
    }

    /// <summary>Outcome of the write step of <see cref="MappingDialogTarget.Save"/>.</summary>
    internal enum MappingWriteResult
    {
        Written,
        Failed,

        /// <summary>
        /// The configuration was written, but reading it back did not give the same mappings. The
        /// write step has already put the original configuration back.
        /// </summary>
        VerifyFailed,
    }

    /// <summary>
    /// The add/edit remapping dialog saves only into the profile it was opened for. The active
    /// profile can change while it is open (another editor, the cycle hotkey, device auto-switch),
    /// and the editor then rebuilds itself for the new profile. An instance holds the profile
    /// captured when the dialog opened; it cannot be changed afterwards. Kept free of UI and native
    /// dependencies so the rule and the order of the save steps can be unit tested.
    /// </summary>
    internal sealed class MappingDialogTarget
    {
        private MappingDialogTarget(string? profile)
        {
            Profile = profile;
        }

        /// <summary>Gets the profile the dialog was opened for.</summary>
        internal string? Profile { get; }

        /// <summary>Captures the profile the editor is showing when the dialog opens.</summary>
        internal static MappingDialogTarget Capture(string? profileShownInEditor) => new(profileShownInEditor);

        /// <summary>
        /// Gets whether the dialog's changes may still be saved: both the profile the editor is
        /// showing and the profile the engine has active must still be the one captured when the
        /// dialog opened. The second check covers the moment after the engine switched and before
        /// the editor followed (its file watcher is debounced).
        /// </summary>
        internal static bool CanSave(string? targetAtOpen, string? editorProfile, string? liveProfile) =>
            !string.IsNullOrWhiteSpace(targetAtOpen) &&
            string.Equals(targetAtOpen, editorProfile, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(targetAtOpen, liveProfile, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc cref="CanSave(string?, string?, string?)"/>
        internal bool CanSave(string? editorProfile, string? liveProfile) => CanSave(Profile, editorProfile, liveProfile);

        /// <summary>
        /// The save transaction, in order: decide whether the captured profile is still the target,
        /// and only then write. Nothing is written when the editor or either freshly loaded service
        /// (<paramref name="originalProfile"/>, <paramref name="candidateProfile"/>) names another
        /// profile. If the profile switches after this check, the verification after the write
        /// reads the new profile and fails; that is reported as a profile change too, when
        /// <paramref name="persistedProfile"/> (the profile the verification read) differs from the
        /// captured one.
        /// </summary>
        internal MappingSaveResult Save(
            string? editorProfile,
            string? originalProfile,
            string? candidateProfile,
            Func<MappingWriteResult> write,
            Func<string?> persistedProfile)
        {
            if (!CanSave(editorProfile, originalProfile) || !CanSave(candidateProfile, candidateProfile))
            {
                return MappingSaveResult.ProfileChanged;
            }

            switch (write())
            {
                case MappingWriteResult.Written:
                    return MappingSaveResult.Saved;

                case MappingWriteResult.VerifyFailed:
                    string? verifiedProfile = persistedProfile();
                    return !string.IsNullOrWhiteSpace(verifiedProfile) &&
                        !string.Equals(verifiedProfile, Profile, StringComparison.OrdinalIgnoreCase)
                        ? MappingSaveResult.ProfileChanged
                        : MappingSaveResult.Failed;

                default:
                    return MappingSaveResult.Failed;
            }
        }
    }
}
