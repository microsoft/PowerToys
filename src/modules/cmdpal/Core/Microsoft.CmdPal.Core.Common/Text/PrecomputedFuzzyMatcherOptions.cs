// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class PrecomputedFuzzyMatcherOptions
{
    public static PrecomputedFuzzyMatcherOptions Default { get; } = new();

    public int CharMatchBonus { get; init; } = 1;

    public int SameCaseBonus { get; init; } = 1;

    public int ConsecutiveMultiplier { get; init; } = 5;

    public int CamelCaseBonus { get; init; } = 2;

    public int StartOfWordBonus { get; init; } = 8;

    public int PathSeparatorBonus { get; init; } = 5;

    public int OtherSeparatorBonus { get; init; } = 4;
}
