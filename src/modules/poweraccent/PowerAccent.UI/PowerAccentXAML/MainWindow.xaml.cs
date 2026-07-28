// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using CoreSize = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class MainWindow : TransparentWindow, IDisposable
{
    // Accent-bar geometry (DIP). Width is derived from the item count (count * ItemWidthDip) plus
    // HorizontalSurfaceOverheadDip (Surface outer margin + its 1px border on each side), not
    // measured from the ListView: its DesiredSize (wrapped in a ScrollViewer) is racy while item
    // containers realize and intermittently reports 0, yielding a blank/clipped bar.
    // The one-row bar hugs its content like the WPF original, capped at the monitor width; beyond
    // that it scrolls and ScrollIntoView reveals the selected glyph.
    private const double RowHeightDip = 92;          // one row of accent pills (item Height=48 + card border)
    private const double DescriptionHeightDip = 36;  // extra row shown when the Unicode description is on
    private const double ItemWidthDip = 48;            // one accent cell (ListViewItem Grid MinWidth=48)
    private const double DescriptionMinWidthDip = 648; // min bar width while the description row shows (WPF parity)

    // Prevents the fractional pixels that may occur with scaled displays from truncating the character list.
    private const double LayoutRoundingDip = 1;

    private readonly Core.PowerAccent _powerAccent;
    private int _selectedIndex = -1;
    private bool _active;

    // The view model lives on the SelectorControl (the x:Bind target); expose it here for the
    // PowerAccent event handlers that populate the accent list and description.
    private SelectorViewModel ViewModel => Selector.ViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Give the overlay a stable UIA identity (window name) for accessibility tools (Narrator,
        // Accessibility Insights) and the release-verification harness. "Quick Accent" is the
        // user-facing feature name.
        AppWindow.Title = "Quick Accent";

        // The accent popup is shown/hidden instantly (no slide/fade) for typing-aid
        // responsiveness. TransientSurface defaults to Transition.None (no animation);
        // SubscribeSurfaceTo forwards to the inner surface so it follows this window's Show/Hide.
        Selector.SubscribeSurfaceTo(this);

        _powerAccent = new Core.PowerAccent(RunOnUiThread);
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectCharacter;

        // No manual theme handling: App.xaml leaves RequestedTheme unset, so WinUI follows the system
        // theme and re-resolves the {ThemeResource} brushes (and retints the acrylic) on a live
        // light/dark switch, even for this never-activated SW_SHOWNA overlay.
    }

    // Marshal keyboard-hook callbacks (ShowToolbar / HideToolbar / NextChar) onto the UI thread. The
    // hook runs on this UI thread, so callbacks arrive here already; run them inline (not via
    // TryEnqueue, which would defer) so the accent injection stays ordered before the hook returns
    // and the trigger key-up propagates. Fall back to enqueueing if ever called off-thread.
    private void RunOnUiThread(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => action());
        }
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        if (!isActive)
        {
            _active = false;

            // Release always-on-top before hiding so the dormant overlay does not keep a discrete
            // GPU awake on hybrid-graphics laptops (issue #34849 / PR #41044). IsAlwaysOnTop is the
            // WinUIEx WindowEx property (same as the sibling PowerDisplay).
            IsAlwaysOnTop = false;
            Hide();
            ViewModel.Characters.Clear();
            _selectedIndex = -1;
            return;
        }

        _active = true;
        ViewModel.ShowDescription = _powerAccent.ShowUnicodeDescription;

        ViewModel.Characters.Clear();
        foreach (var c in chars)
        {
            ViewModel.Characters.Add(c);
        }

        Selector.SetSelectedIndex(_selectedIndex);
        ViewModel.Description = (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
            ? _powerAccent.CharacterDescriptions[_selectedIndex]
            : string.Empty;

        // Always-on-top only while shown, so the overlay sits above the foreground app (Show uses
        // SW_SHOWNA and never activates it); released on hide (see above). Then size and show.
        IsAlwaysOnTop = true;
        SizeAndPosition();
        Show();

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_active)
            {
                Selector.ScrollSelectedIntoView(_selectedIndex);
            }
        });

        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Core.Telemetry.PowerAccentShowAccentMenuEvent());
    }

    private void PowerAccent_OnSelectCharacter(int index, string character)
    {
        _selectedIndex = index;
        Selector.SetSelectedIndex(index);

        if (index >= 0 && index < _powerAccent.CharacterDescriptions.Length)
        {
            ViewModel.Description = _powerAccent.CharacterDescriptions[index];
        }

        Selector.ScrollSelectedIntoView(index);
    }

    private void SizeAndPosition()
    {
        // Width hugs the content: item count * ItemWidthDip plus the space outside the ListView (see
        // the class-level note on why the ListView is not measured), capped at the monitor's max
        // usable width so long lists scroll.
        double maxWidthDip = _powerAccent.GetDisplayMaxWidth();
        double contentWidthDip = (ViewModel.Characters.Count * ItemWidthDip) + Selector.HorizontalSurfaceOverheadDip + LayoutRoundingDip;

        // The Unicode description row needs room for a readable line; the WPF original gave it a
        // 600px MinWidth. Widen a short accent bar to match when the row is shown (the accent bar
        // itself stays centered within the wider window).
        if (ViewModel.ShowDescription)
        {
            contentWidthDip = Math.Max(contentWidthDip, DescriptionMinWidthDip);
        }

        double widthDip = Math.Clamp(contentWidthDip, ItemWidthDip, maxWidthDip);
        double heightDip = RowHeightDip + (ViewModel.ShowDescription ? DescriptionHeightDip : 0);

        // Calculation works in physical pixels; GetDisplayCoordinates multiplies the DIP size by
        // the active monitor's DPI internally and returns the physical top-left for the anchor.
        var coordinates = _powerAccent.GetDisplayCoordinates(new CoreSize(widthDip, heightDip));

        var display = DisplayArea.GetFromPoint(
            new PointInt32((int)Math.Round(coordinates.X), (int)Math.Round(coordinates.Y)),
            DisplayAreaFallback.Nearest);

        double dpiScale = FlyoutWindowHelper.GetDpiScale(display);

        var rect = new RectInt32(
            (int)Math.Round(coordinates.X),
            (int)Math.Round(coordinates.Y),
            (int)Math.Ceiling(widthDip * dpiScale),
            (int)Math.Ceiling(heightDip * dpiScale));

        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, display, rect);
    }

    public void Dispose()
    {
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
        GC.SuppressFinalize(this);
    }
}
