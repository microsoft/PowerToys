// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Run-style Tab routing: land on the selected row first, then cycle hover icons.
/// </summary>
public static class HoverActionTabNavigation
{
    /// <summary>
    /// Forward Tab from search: row highlight first, then hover icons.
    /// </summary>
    public static bool TryHandleForward(ref int selectedIndex, ref bool rowTabFocused, int actionCount, bool stripVisible)
    {
        if (!stripVisible || actionCount <= 0)
        {
            return false;
        }

        if (selectedIndex == HoverActionSelection.NoSelection && !rowTabFocused)
        {
            rowTabFocused = true;
            return true;
        }

        rowTabFocused = false;
        return HoverActionSelection.TrySelectNext(ref selectedIndex, actionCount, stripVisible);
    }

    /// <summary>
    /// Backward Tab from search: exit row step, or move within / into hover icons.
    /// </summary>
    public static bool TryHandleBackward(ref int selectedIndex, ref bool rowTabFocused, int actionCount, bool stripVisible)
    {
        if (!stripVisible || actionCount <= 0)
        {
            return false;
        }

        if (selectedIndex != HoverActionSelection.NoSelection)
        {
            if (HoverActionSelection.TrySelectPrev(ref selectedIndex, actionCount, stripVisible))
            {
                return true;
            }

            rowTabFocused = true;
            return true;
        }

        if (rowTabFocused)
        {
            rowTabFocused = false;
            return false;
        }

        return HoverActionSelection.TrySelectLastOnBackwardEntry(ref selectedIndex, actionCount, stripVisible);
    }
}
