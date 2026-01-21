// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Represents a span of matched text for highlighting.
/// </summary>
/// <param name="Start">The starting index of the match.</param>
/// <param name="Length">The length of the match.</param>
public readonly record struct MatchSpan(int Start, int Length);
