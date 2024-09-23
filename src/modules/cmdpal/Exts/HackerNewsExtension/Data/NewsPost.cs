// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace HackerNewsExtension.Data;

public sealed class NewsPost
{
    public string Title { get; init; } = string.Empty;

    public string Link { get; init; } = string.Empty;

    public string CommentsLink { get; init; } = string.Empty;

    public string Poster { get; init; } = string.Empty;
}
