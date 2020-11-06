#include "pch.h"
#include "UIHelpers.h"

namespace UIHelpers
{
    // This method sets focus to the first Type button on the last row of the Grid
    void SetFocusOnTypeButtonInLastRow(StackPanel& parent, long colCount)
    {
        // First element in the last row (StackPanel)
        StackPanel firstElementInLastRow = parent.Children().GetAt(parent.Children().Size() - 1).as<StackPanel>().Children().GetAt(0).as<StackPanel>();

        // Type button is the first child in the StackPanel
        Button firstTypeButtonInLastRow = firstElementInLastRow.Children().GetAt(0).as<Button>();

        // Set programmatic focus on the button
        firstTypeButtonInLastRow.Focus(FocusState::Programmatic);
    }
}
