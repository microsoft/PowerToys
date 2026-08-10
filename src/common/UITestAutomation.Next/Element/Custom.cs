// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Custom control (UIA ControlType <c>Custom</c>) — used by bespoke surfaces like FancyZones
/// zones and Workspaces canvases. Inherits drag from <see cref="Element"/>.
/// </summary>
public class Custom : Element
{
    public Custom()
    {
        TargetControlType = "Custom";
    }
}
