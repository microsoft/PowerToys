// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
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
    // window narrower than its own content (issue #49488). The measurement is taken after the
    // surface has been laid out, which the cloaked window (see the constructor) makes possible
    // before anything is on screen.
    private const double RowHeightDip = 92;            // one row of accent pills (item Height=48 + card border)
    private const double DescriptionHeightDip = 36;    // extra row shown when the Unicode description is on
    private const double MinItemWidthDip = 48;         // one accent cell's minimum (ListViewItem MinWidth=48)
    private const double DescriptionMinWidthDip = 648; // min bar width while the description row shows (WPF parity)

    // Prevents the fractional pixels that may occur with scaled displays from truncating the character list.
    private const double LayoutRoundingDip = 1;

    private readonly Core.PowerAccent _powerAccent;
    private int _selectedIndex = -1;
    private int _showGeneration;

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

        // Cloak the overlay instead of hiding it. A hidden WinUI 3 window renders nothing, so the
        // bar would become visible while the characters of the new summon are still un-laid-out and
        // the first frames would show the previous summon's content - issue #49489. Cloaked, the
        // window stays shown (and therefore laid out and painted) while invisible, so every summon
        // builds its bar out of sight and Reveal() never has a wrong frame to put on screen. This
        // also warms the XAML tree up now rather than on the first summon.
        EnableCloakedHide();

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
            // Invalidate any layout callback still queued for the summon being dismissed, so it
            // cannot reveal a bar that is on its way out. Every dismissal bumps the counter and
            // every summon captures it, so this is the only liveness check the callback needs.
            _showGeneration++;

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

        // Always-on-top only while shown, so the overlay sits above the foreground app (Show uses
        // SW_SHOWNA and never activates it); released on hide (see above). The window is cloaked at
        // this point, so Show() flips the surface out of Collapsed and lets the new bar lay out
        // without anything reaching the screen.
        IsAlwaysOnTop = true;
        Show();

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            if (generation != _showGeneration)
            {
                return;
            }

            // Runs after TransparentWindow.Show has flipped the surface out of Collapsed, so this is
            // the first point at which the bar can lay out at all - a Collapsed subtree is never
            // measured, which is why the size is taken here and not before Show.
            Selector.UpdateLayout();
            SizeAndPosition(Selector.MeasureContentWidthDip());

            // Lay out again at the new window size: ScrollIntoView needs realized containers and the
            // final viewport to land on the right offset.
            Selector.UpdateLayout();
            Selector.ScrollSelectedIntoView(_selectedIndex);

            // UpdateLayout only completes XAML measure/arrange. Wait for the composition commit so
            // uncloaking cannot expose the previous summon's redirection surface at the new bounds.
            try
            {
                await ElementCompositionPreview.GetElementVisual(Selector).Compositor.RequestCommitAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to commit the Quick Accent layout before reveal", ex);
                return;
            }

            if (generation != _showGeneration)
            {
                return;
            }

            // Everything above happened on a window that was shown but cloaked. The commit ensures
            // DWM's redirection surface already contains this summon's final layout before it is
            // exposed, so Reveal cannot flash the previous bar at the new bounds (issue #49489).
            Reveal();
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

    public void Dispose()
    {
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
        GC.SuppressFinalize(this);
    }
}
