// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>WinUI NavigationViewItem surfaces as ControlType.ListItem.</summary>
public class NavigationViewItem : Element
{
    public NavigationViewItem()
    {
        TargetControlType = "ListItem";
    }
}
