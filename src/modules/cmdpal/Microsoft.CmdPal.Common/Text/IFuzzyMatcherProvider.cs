// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Text;

public interface IFuzzyMatcherProvider
{
    IPrecomputedFuzzyMatcher Current { get; }

    void UpdateSettings(PrecomputedFuzzyMatcherOptions core, PinyinFuzzyMatcherOptions? pinyin = null);
}
