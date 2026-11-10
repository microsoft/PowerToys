// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>WinUI/WPF <c>Pane</c> (UIA ControlType <c>Pane</c>). Inherits drag from <see cref="Element"/>.</summary>
public class Pane : Element
{
    public Pane()
    {
        TargetControlType = "Pane";
    }
}
