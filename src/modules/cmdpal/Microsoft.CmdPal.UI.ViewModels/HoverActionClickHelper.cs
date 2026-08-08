// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Hit-test helpers for list-row hover action strips (28px icon + 2px gap).
/// </summary>
public static class HoverActionClickHelper
{
    public const double SlotWidth = 30;

    public static int GetActionIndexFromX(double x) => x < 0 ? -1 : (int)(x / SlotWidth);

    public static bool IsPointInsideHoverList(double x, double y, double width, double height) =>
        x >= 0 && y >= 0 && x <= width && y <= height;

    public static T? TryGetActionAtIndex<T>(IReadOnlyList<T> actions, double x)
    {
        var index = GetActionIndexFromX(x);
        if (index < 0 || index >= actions.Count)
        {
            return default;
        }

        return actions[index];
    }
}
