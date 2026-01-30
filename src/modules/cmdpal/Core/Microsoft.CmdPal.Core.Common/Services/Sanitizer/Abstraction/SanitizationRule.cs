// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

public readonly record struct SanitizationRule
{
    public SanitizationRule(Regex regex, string replacement, string description = "")
    {
        Regex = regex;
        Replacement = replacement;
        Evaluator = null;
        Description = description;
    }

    public SanitizationRule(Regex regex, MatchEvaluator evaluator, string description = "")
    {
        Regex = regex;
        Evaluator = evaluator;
        Replacement = null;
        Description = description;
    }

    public Regex Regex { get; }

    public string? Replacement { get; }

    public MatchEvaluator? Evaluator { get; }

    public string Description { get; }

    public override string ToString() => $"{Description}: {Regex} -> {Replacement ?? "<evaluator>"}";
}
