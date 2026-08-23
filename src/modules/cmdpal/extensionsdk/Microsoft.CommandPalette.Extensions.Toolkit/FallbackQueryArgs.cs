// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Contains the input for one fallback query.
/// </summary>
public partial class FallbackQueryArgs : IFallbackQueryArgs
{
    private readonly PropertySet _properties = new();

    public FallbackQueryArgs(string query, string queryId, uint requestedItemCount, string[]? languageTags = null)
    {
        Query = query;
        QueryId = queryId;
        RequestedItemCount = requestedItemCount;
        LanguageTags = languageTags ?? [];
    }

    public string Query { get; }

    public string QueryId { get; }

    public uint RequestedItemCount { get; }

    public string[] LanguageTags { get; }

    public IDictionary<string, object> GetProperties() => _properties;
}
