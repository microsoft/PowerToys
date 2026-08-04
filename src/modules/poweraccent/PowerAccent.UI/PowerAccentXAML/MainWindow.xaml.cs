// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using CoreSize = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class MainWindow : TransparentWindow, IDisposable
{
    // Accent-bar geometry (DIP). The one-row bar hugs its content like the WPF original, capped at
    // the monitor's max usable width; beyond that it scrolls and ScrollIntoView reveals the selected
    // glyph. Its width is measured from the character list (SelectorControl.MeasureContentWidthDip)
    // plus the space outside the list, NOT derived from the item count: an accent cell is a MINIMUM
    // of 48 DIP, so a glyph wider than that grows its cell and a count * 48 estimate would size the
    // window narrower than its own content (issue #49488). The bar is sized twice per summon - once
    // before Show, then again after the first real layout pass, because only the second measurement
    // is taken on a templated, non-collapsed subtree.
    private const double RowHeightDip = 92;            // one row of accent pills (item Height=48 + card border)
    private const double DescriptionHeightDip = 36;    // extra row shown when the Unicode description is on
    private const double MinItemWidthDip = 48;         // one accent cell's minimum (ListViewItem MinWidth=48)
    private const double DescriptionMinWidthDip = 648; // min bar width while the description row shows (WPF parity)

    // Prevents the fractional pixels that may occur with scaled displays from truncating the character list.
    private const double LayoutRoundingDip = 1;

    // Upper bound on how long the bar may stay invisible while waiting for its first composed frame
    // (see PowerAccent_OnChangeDisplay). A CompositionTarget.Rendering handler forces the UI thread
    // to run every frame, so this timer is NOT the normal path - that is FramesBeforeReveal refresh
    // intervals. It exists because the tick cadence carries no guarantee (microsoft-ui-xaml#11048)
    // and because ticking can stop for a locked or fully occluded session. Treat it as a floor, not
    // a deadline: DispatcherQueueTimer tasks run at a priority lower than idle. On that path the bar
    // simply appears the way it used to.
    private const int RevealTimeoutMs = 150;

    // Composed frames to wait before unveiling. The bar is transparent until Reveal(), so neither of
    // these frames draws it; they buy settling time for the resize below and for the surface's
    // Collapsed -> Visible flip, so that the frame which first rasterizes the bar (the one after
    // Opacity = 1) already has the correct client area. Rendering is not tied to any specific
    // element, so a tick is evidence that a frame elapsed - not that this subtree was composed.
    private const int FramesBeforeReveal = 2;

    private readonly Core.PowerAccent _powerAccent;
    private readonly DispatcherQueueTimer _revealTimer;
    private int _selectedIndex = -1;
    private int _showGeneration;
    private int _revealGeneration = -1;
    private double _measuredContentWidthDip = -1;
    private int _renderedFrames;
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

        _revealTimer = DispatcherQueue.CreateTimer();
        _revealTimer.IsRepeating = false;
        _revealTimer.Interval = TimeSpan.FromMilliseconds(RevealTimeoutMs);
        _revealTimer.Tick += (_, _) => Reveal();

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
            _showGeneration++;

            // Drop any reveal still pending for the summon being dismissed. Same motivation as
            // releasing always-on-top below: the Rendering handler forces the UI thread to run every
            // frame, so it must not outlive the visible bar.
            CancelPendingReveal();

            // Release always-on-top before hiding so the dormant overlay does not keep a discrete
            // GPU awake on hybrid-graphics laptops (issue #34849 / PR #41044). IsAlwaysOnTop is the
            // WinUIEx WindowEx property (same as the sibling PowerDisplay).
            IsAlwaysOnTop = false;
            Hide();
            _selectedIndex = -1;

            // The characters are deliberately left in place. Hide() only queues the dismissal, so
            // clearing them here empties the list while the window is still on screen, and that
            // blank bar is exactly the frame the next summon would start from. The next summon
            // clears and refills them anyway.
            return;
        }

        _active = true;
        int generation = ++_showGeneration;
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

        // Show the bar transparent and unveil it once it has actually been drawn. A hidden WinUI 3
        // window renders nothing, so the HWND would otherwise become visible while the new
        // characters are still un-laid-out, and the first frames would show the previous bar at its
        // previous size, clipped by the new (already correct) client area - issue #49489. This is
        // the WinUI 3 counterpart of the WPF fix in #46593, which rendered the toolbar off screen
        // and only then moved it into view.
        Selector.Opacity = 0;

        // Always-on-top only while shown, so the overlay sits above the foreground app (Show uses
        // SW_SHOWNA and never activates it); released on hide (see above). Then size and show.
        IsAlwaysOnTop = true;
        SizeAndPosition(Selector.MeasureContentWidthDip());
        Show();

        // Arm the fallback deadline synchronously: the bar is transparent from here on, so the
        // timeout has to be running even if the callback below never gets to run - otherwise a
        // dropped callback leaves the bar invisible for the whole summon instead of merely late.
        ArmRevealTimeout(generation);

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!_active || generation != _showGeneration)
            {
                return;
            }

            // Runs after TransparentWindow.Show has made the window visible and flipped the surface
            // out of Collapsed, so this is the first point at which the bar can lay out at all -
            // and ScrollIntoView needs realized containers to land on the right offset.
            Selector.UpdateLayout();

            // The measurement above ran before the surface left Collapsed and, on the first summon
            // of the process, before its template had ever been applied, so it can report less than
            // the items really need - in which case GetToolbarWidth silently falls back to the
            // item-count estimate this whole change exists to replace. Now that a real layout pass
            // has run, re-measure and re-size when the two disagree. The bar is still at Opacity 0,
            // so the correction is never seen as a resize.
            double laidOutContentWidthDip = Selector.MeasureContentWidthDip();
            if (Math.Abs(laidOutContentWidthDip - _measuredContentWidthDip) > LayoutRoundingDip)
            {
                SizeAndPosition(laidOutContentWidthDip);
                Selector.UpdateLayout();
            }

            Selector.ScrollSelectedIntoView(_selectedIndex);
            WaitForFirstFrameThenReveal();
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

    private void SizeAndPosition(double measuredContentWidthDip)
    {
        // Width hugs the content: the measured character list plus the space outside it (see the
        // class-level note), floored at one minimum cell per character and capped at the monitor's
        // max usable width so long lists scroll. The Unicode description row needs room for a
        // readable line, so it widens a short bar to the WPF original's minimum (the accent bar
        // itself stays centered within the wider window).
        _measuredContentWidthDip = measuredContentWidthDip;

        double widthDip = _powerAccent.GetDisplayWidth(
            measuredContentWidthDip,
            ViewModel.Characters.Count,
            MinItemWidthDip,
            Selector.HorizontalSurfaceOverheadDip + LayoutRoundingDip,
            ViewModel.ShowDescription ? DescriptionMinWidthDip : 0);

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

    // Starts the fallback deadline for the reveal, tagged with the summon that armed it.
    private void ArmRevealTimeout(int generation)
    {
        // Cancel before re-tagging, not after. The Stop() inside CancelPendingReveal is what kills
        // the previous summon's watchdog, and that watchdog was the only remaining path that would
        // have driven its still-attached Rendering handler through Reveal(). Leaving the handler on
        // would let it unveil THIS summon after FramesBeforeReveal ticks - before the layout
        // callback below has run - and the generation guard in Reveal() cannot reject it, because
        // the two assignments underneath put _revealGeneration and _showGeneration back in sync.
        CancelPendingReveal();

        _revealGeneration = generation;
        _renderedFrames = 0;

        // Restart rather than extend: DispatcherQueueTimer does not document what Start() does to a
        // timer that is already running, so the deadline is reset explicitly.
        _revealTimer.Start();
    }

    // Drops a pending reveal. The watchdog and the per-frame handler always come off together: the
    // handler forces the UI thread to run every frame, and the watchdog is what guarantees it is
    // detached on a session where no frames arrive at all.
    private void CancelPendingReveal()
    {
        _revealTimer.Stop();
        CompositionTarget.Rendering -= OnRenderingBeforeReveal;
    }

    // Unveils the bar once the compositor has drawn it, or after RevealTimeoutMs if it never does.
    private void WaitForFirstFrameThenReveal()
    {
        _renderedFrames = 0;

        // Re-arms rather than stacks: a summon that lands while an earlier one is still waiting
        // reuses the same handler, so there is only ever one pending reveal.
        CompositionTarget.Rendering -= OnRenderingBeforeReveal;
        CompositionTarget.Rendering += OnRenderingBeforeReveal;
    }

    private void OnRenderingBeforeReveal(object sender, object e)
    {
        if (++_renderedFrames < FramesBeforeReveal)
        {
            return;
        }

        Reveal();
    }

    private void Reveal()
    {
        // Unconditional: the handler forces the UI thread to run every frame, so it has to come off
        // even when the reveal itself is dropped as stale just below.
        CancelPendingReveal();

        // Same guard as the layout callback. A reveal armed by an earlier summon must not unveil a
        // newer one before its own layout pass has run - that is exactly the stale frame of #49489.
        if (_active && _revealGeneration == _showGeneration)
        {
            Selector.Opacity = 1;
        }
    }

    public void Dispose()
    {
        CancelPendingReveal();
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
        GC.SuppressFinalize(this);
    }
}
