// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal static class CompactModeLayout
{
    public static bool ShouldUpdateForPageChange(
        bool isRootPage,
        bool queryChanged,
        bool contentChanged)
    {
        return queryChanged || (!isRootPage && contentChanged);
    }

    public static bool ShouldExpand(
        bool compactMode,
        bool isTransient,
        bool isNested,
        bool hasQuery,
        bool hasPageContent)
    {
        return !compactMode ||
            isTransient ||
            hasQuery ||
            (isNested && hasPageContent);
    }
}
