#pragma once

// This namespace contains UI methods that are to be used for both KBM windows
namespace UIHelpers
{
    // This method sets focus to the first Type button on the last row of the Grid
    void SetFocusOnTypeButtonInLastRow(StackPanel& parent, long colCount);

    RECT GetForegroundWindowDesktopRect();
}
