// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Pure keyboard-selection helpers for list-row hover action strips (Run-style Tab cycling).
/// </summary>
public static class HoverActionSelection
{
    public const int NoSelection = -1;

    /// <summary>
    /// Advances Tab selection within the hover strip.
    /// </summary>
    /// <returns><c>true</c> when Tab should stay on the strip; <c>false</c> when focus may leave.</returns>
    public static bool TrySelectNext(ref int selectedIndex, int actionCount, bool stripVisible)
    {
        if (!stripVisible || actionCount <= 0)
        {
            return false;
        }

        if (selectedIndex >= actionCount - 1)
        {
            selectedIndex = NoSelection;
            return false;
        }

        selectedIndex++;
        return true;
    }

    /// <summary>
    /// Moves backward within the hover strip (Shift+Tab).
    /// </summary>
    /// <returns><c>true</c> when Tab should stay on the strip; <c>false</c> when focus may leave.</returns>
    public static bool TrySelectPrev(ref int selectedIndex, int actionCount, bool stripVisible)
    {
        if (!stripVisible || actionCount <= 0)
        {
            return false;
        }

        if (selectedIndex <= NoSelection)
        {
            return false;
        }

        if (selectedIndex == 0)
        {
            selectedIndex = NoSelection;
            return false;
        }

        selectedIndex--;
        return true;
    }

    /// <summary>
    /// Run parity: first Shift+Tab into a row with actions lands on the last icon.
    /// </summary>
    public static bool TrySelectLastOnBackwardEntry(ref int selectedIndex, int actionCount, bool stripVisible)
    {
        if (!stripVisible || actionCount <= 0 || selectedIndex != NoSelection)
        {
            return false;
        }

        selectedIndex = actionCount - 1;
        return true;
    }
}
