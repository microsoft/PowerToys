// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Tab control (UIA ControlType <c>Tab</c>). Inherits drag from <see cref="Element"/> for
/// tab-reorder / tear-off scenarios (see <see cref="Element.KeyDownAndDrag"/>).
/// </summary>
public class Tab : Element
{
    public Tab()
    {
        TargetControlType = "Tab";
    }
}
