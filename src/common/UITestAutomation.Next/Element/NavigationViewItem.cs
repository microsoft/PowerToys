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

    /// <summary>
    /// Activate the item with a coordinate-free UIA invoke instead of the base's physical mouse click.
    /// NavigationViewItems live in a scrollable nav pane (and collapse into an overflow "…" menu when
    /// the window is narrow), so an item can report a size yet sit outside the visible viewport — a real
    /// click at those bounds would miss. Navigation is also usually the FIRST interaction right after a
    /// settings window appears, when the window exists but isn't yet interactive-for-mouse-input for a
    /// moment, so a physical click races that transition and is silently dropped (observed as a flaky
    /// "NavigationViewItem not found" on slower Win10 agents). UIA activation (SelectionItem/Invoke
    /// pattern) selects the page regardless of scroll position, foreground, or that ready-state race.
    /// </summary>
    public override void Click(bool rightClick = false, int msPostAction = 200, int timeoutMS = 5000)
    {
        Invoke(rightClick, msPostAction);
    }
}
