// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI/WPF <c>CheckBox</c> (UIA ControlType <c>CheckBox</c>). State is read via
/// <c>winapp ui get-property ToggleState</c> and changed via <c>winapp ui invoke</c>.
/// </summary>
public class CheckBox : Element
{
    public CheckBox()
    {
        TargetControlType = "CheckBox";
    }

    /// <summary>True when UIA <c>ToggleState</c> is <c>On</c> (<c>Indeterminate</c> reads as not-checked).</summary>
    public bool IsChecked => string.Equals(GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);

    /// <summary>Flip to <paramref name="value"/> only if currently different.</summary>
    public CheckBox SetCheck(bool value = true)
    {
        if (IsChecked != value)
        {
            Click();
        }

        return this;
    }
}
