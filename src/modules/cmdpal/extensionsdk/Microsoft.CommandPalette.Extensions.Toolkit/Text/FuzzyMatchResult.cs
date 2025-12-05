// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
///     Result of a fuzzy match operation.
/// </summary>
public readonly struct FuzzyMatchResult
{
    public readonly int Score;
    public readonly int[] Positions;

    public static readonly FuzzyMatchResult NoMatch = new(0, []);

    public FuzzyMatchResult(int score, int[] positions)
    {
        Score = score;
        Positions = positions;
    }

    public bool IsMatch => Score > 0;
}
