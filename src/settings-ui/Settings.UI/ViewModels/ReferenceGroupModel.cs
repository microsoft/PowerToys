// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Represents a group of language entries in the Quick Accent reference guide,
    /// with a localised group header.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IEnumerable{T}"/> so that a WinUI 3
    /// <c>CollectionViewSource</c> with <c>IsSourceGrouped="True"</c> can use this
    /// type directly as a group - the view source enumerates the group to get its
    /// items and uses the group object itself as the group header.
    /// </remarks>
    public sealed class ReferenceGroupModel : IEnumerable<ReferenceLanguageModel>
    {
        /// <summary>Gets the localised display name for this group.</summary>
        public string GroupHeader { get; init; }

        /// <summary>Gets the language entries belonging to this group.</summary>
        public IReadOnlyList<ReferenceLanguageModel> Languages { get; init; }

        /// <inheritdoc/>
        public IEnumerator<ReferenceLanguageModel> GetEnumerator() => Languages.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
