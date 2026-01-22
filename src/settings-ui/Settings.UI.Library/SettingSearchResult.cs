// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using Common.Search;

namespace Settings.UI.Library
{
    /// <summary>
    /// Represents a search result for a settings entry with scoring metadata.
    /// </summary>
    public sealed class SettingSearchResult
    {
        public required SettingEntry Entry { get; init; }

        public required double Score { get; init; }

        public required SearchMatchKind MatchKind { get; init; }

        public IReadOnlyList<MatchSpan>? MatchSpans { get; init; }
    }
}
