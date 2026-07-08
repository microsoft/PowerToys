// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI/WPF <c>Slider</c> (UIA ControlType <c>Slider</c>). Reads and writes the value directly
/// through the CLI (<c>winapp ui get-value</c> / <c>set-value</c>, RangeValuePattern) — no
/// arrow-key stepping like the legacy harness.
/// </summary>
public class Slider : Element
{
    public Slider()
    {
        TargetControlType = "Slider";
    }

    /// <summary>Current value via <c>winapp ui get-value</c>. Returns 0 when it can't be parsed.</summary>
    public double Value
    {
        get
        {
            var raw = GetValue();
            return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0d;
        }
    }

    /// <summary>Set the value directly via <c>winapp ui set-value</c> (RangeValuePattern).</summary>
    public Slider SetValue(double value)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess(
            "ui", "set-value", Selector,
            value.ToString(CultureInfo.InvariantCulture),
            Owner!.TargetFlag, Owner!.TargetValue);
        return this;
    }
}
