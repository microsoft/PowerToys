// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Resize/move <c>Thumb</c> (UIA ControlType <c>Thumb</c>), e.g. a splitter or slider handle.
/// Inherits drag from <see cref="Element"/>.
/// </summary>
public class Thumb : Element
{
    public Thumb()
    {
        TargetControlType = "Thumb";
    }
}
