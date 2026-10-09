// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI/WPF <c>RadioButton</c> (UIA ControlType <c>RadioButton</c>). Selected state is read via
/// <c>winapp ui get-property IsSelected</c>; selection is performed via <c>winapp ui invoke</c>.
/// </summary>
public class RadioButton : Element
{
    public RadioButton()
    {
        TargetControlType = "RadioButton";
    }

    /// <summary>True when this radio button is the selected option (UIA SelectionItemPattern.IsSelected).</summary>
    public bool IsSelected => Selected;

    /// <summary>Select this radio button if it isn't already selected.</summary>
    public RadioButton Select()
    {
        if (!IsSelected)
        {
            Click();
        }

        return this;
    }
}
