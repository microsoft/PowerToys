// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI <c>ToggleSwitch</c> surfaces as <c>ControlType.Button</c> + <c>ClassName="ToggleSwitch"</c>.
/// Pinning <see cref="Element.TargetClassName"/> avoids picking up sibling Buttons with the same Name
/// (e.g. the module's navigation card on the dashboard).
/// </summary>
public class ToggleSwitch : Button
{
    public ToggleSwitch()
    {
        TargetClassName = "ToggleSwitch";
    }

    /// <summary>Reads UIA <c>ToggleState</c> via winappcli and compares to <c>"On"</c>.</summary>
    public bool IsOn => string.Equals(GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);

    /// <summary>Flip to <paramref name="value"/> only if currently different.</summary>
    public ToggleSwitch Toggle(bool value = true)
    {
        if (IsOn != value)
        {
            Click();
        }

        return this;
    }
}
