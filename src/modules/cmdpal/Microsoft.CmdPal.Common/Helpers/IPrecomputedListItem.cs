// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Represents an item that can provide precomputed fuzzy matching targets for its title and subtitle.
/// </summary>
public interface IPrecomputedListItem
{
    /// <summary>
    /// Gets the fuzzy matching target for the item's title.
    /// </summary>
    /// <param name="matcher">The precomputed fuzzy matcher used to build the target.</param>
    /// <returns>The fuzzy target for the title.</returns>
    FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher);

    /// <summary>
    /// Gets the fuzzy matching target for the item's subtitle.
    /// </summary>
    /// <param name="matcher">The precomputed fuzzy matcher used to build the target.</param>
    /// <returns>The fuzzy target for the subtitle.</returns>
    FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher);
}
