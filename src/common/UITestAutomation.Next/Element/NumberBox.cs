// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// WinUI <c>NumberBox</c>. Windows App SDK versions expose it through different UIA control types,
/// so callers should bind it by a stable accessibility ID rather than a control-type filter.
/// </summary>
public class NumberBox : Element
{
    /// <summary>Set the value directly through UIA ValuePattern or RangeValuePattern.</summary>
    public NumberBox SetValue(double value)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess(
            "ui", "set-value", Selector,
            value.ToString(CultureInfo.InvariantCulture),
            Owner!.TargetFlag, Owner!.TargetValue);
        return this;
    }
}
