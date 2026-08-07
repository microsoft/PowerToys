// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Represents a language entry in the Quick Accent reference guide, including its
    /// display name, key mappings, and whether the user has currently selected it.
    /// </summary>
    public sealed class ReferenceLanguageModel
    {
        /// <summary>Gets the localised display name for this language.</summary>
        public string DisplayName { get; init; }

        /// <summary>Gets a value indicating whether this language is currently selected by the user.</summary>
        public bool IsSelected { get; init; }

        /// <summary>Gets the key-to-characters mappings declared for this language.</summary>
        public IReadOnlyList<KeyMappingModel> KeyMappings { get; init; }
    }
}
