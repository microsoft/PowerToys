// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// A choice in the auto-switch profile picker: a profile identified by its stable id, shown with a
    /// separate display label. The "unassigned" sentinel has a <see langword="null"/> <see cref="Id"/>,
    /// so it is distinguished by identity rather than by its localized text — a real profile whose name
    /// happens to equal the "(not assigned)" label can therefore still have its assignment persisted.
    /// </summary>
    public sealed class ProfileChoice
    {
        public ProfileChoice(string? id, string display)
        {
            Id = id;
            Display = display;
        }

        /// <summary>Stable profile id (the config filename stem), or <see langword="null"/> for the unassigned sentinel.</summary>
        public string? Id { get; }

        /// <summary>Text shown in the picker.</summary>
        public string Display { get; }
    }
}
