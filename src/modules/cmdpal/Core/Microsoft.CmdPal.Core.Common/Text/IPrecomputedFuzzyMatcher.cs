// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Text;

public interface IPrecomputedFuzzyMatcher
{
    uint SchemaId { get; }

    FuzzyQuery PrecomputeQuery(string? input);

    FuzzyTarget PrecomputeTarget(string? input);

    int Score(scoped in FuzzyQuery query, scoped in FuzzyTarget target);
}
