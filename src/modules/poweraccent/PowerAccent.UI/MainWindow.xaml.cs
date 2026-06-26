// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Flyout;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using CoreSize = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class MainWindow : TransparentWindow, IDisposable
{
    // Deterministic accent-bar geometry (DIP). We deliberately do NOT measure the ListView to size
    // the window: a ListView wraps its items in a ScrollViewer (whose DesiredSize does not reflect
    // content size), and measuring it before/while its item containers realize is racy (it
    // intermittently reports 0, yielding a blank/clipped bar). Instead the width is derived from the
    // item count (count * ItemWidthDip) so the one-row bar hugs its content like the WPF original,
    // capped at the monitor's max width; beyond that the ListView scrolls horizontally and
    // ScrollIntoView reveals the selected glyph.
    private const double RowHeightDip = 52;          // one row of accent pills (item Height=48 + card border)
    private const double DescriptionHeightDip = 48;  // extra row shown when the Unicode description is on
    private const double ItemWidthDip = 48;            // one accent cell (ListViewItem Grid MinWidth=48)
    private const double DescriptionMinWidthDip = 600; // min bar width while the description row shows (WPF parity)

    private readonly Core.PowerAccent _powerAccent;
    private readonly ThemeListener _themeListener;
    private int _selectedIndex = -1;
    private bool _active;

    public SelectorViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        // x:Bind bindings on a Window-rooted XAML are initialized ONLY by Window.Activated (the
        // generated Connect does `element1.Activated += bindings.Activated`). This accent popup is
        // shown with SW_SHOWNA and is deliberately never activated, so Activated never fires and the
        // root x:Bind bindings (ItemsSource, Description, DescriptionVisibility) would stay unset -
        // the ListView renders empty. Force the one-time binding init here; OneWay tracking then
        // keeps them live (the ListView follows the ObservableCollection, text follows the VM).
        Bindings.Update();

        // Give the overlay a stable UIA identity (window name) for accessibility tools (Narrator,
        // Accessibility Insights) and the release-verification harness. "Quick Accent" is the
        // user-facing feature name.
        AppWindow.Title = "Quick Accent";

        // The accent popup overlays the app being typed into and must never steal focus
        // (TransparentWindow.Show uses SW_SHOWNA). Without always-on-top it is shown correctly
        // sized and positioned but BEHIND the foreground app, so it is effectively invisible.
        // The WPF original set Window.Topmost on every show; the WinUI 3 equivalent is the
        // presenter's IsAlwaysOnTop.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }
        else
        {
            Logger.LogWarning($"Quick Accent selector presenter is not an OverlappedPresenter ({AppWindow.Presenter?.GetType().Name}); the popup cannot be made always-on-top and may appear behind the active app.");
        }

        // The accent popup is shown/hidden instantly (no slide/fade) for typing-aid
        // responsiveness. TransientSurface defaults to Transition.None (no animation);
        // SubscribeTo wires the surface to this window's Show/Hide so it follows along.
        Surface.SubscribeTo(this);

        _powerAccent = new Core.PowerAccent(action => DispatcherQueue.TryEnqueue(() => action()));
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectCharacter;

        // The accent popup is a long-lived, never-activated SW_SHOWNA overlay, so it does not get
        // WinUI 3's automatic runtime theme switching the way an activated window would. Mirror the
        // WPF original's ThemeMode="System" by following the system app theme explicitly: apply it
        // now and on every change. ThemeListener raises ThemeChanged from a WMI background thread,
        // so the handler marshals back to the UI thread before touching XAML.
        try
        {
            _themeListener = new ThemeListener();
            _themeListener.ThemeChanged += (_) => DispatcherQueue.TryEnqueue(ApplyTheme);
        }
        catch (Exception ex)
        {
            Logger.LogError("Quick Accent could not start the theme listener; the accent popup will not follow live system theme changes.", ex);
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        // Drive the whole surface (border + acrylic + inner content) from the system app theme.
        // Surface is the window's root content (xamlRoot.Content), so setting RequestedTheme flips
        // its ActualTheme, which re-resolves {ThemeResource} brushes and fires ActualThemeChanged -
        // the signal AlwaysActiveDesktopAcrylicBackdrop uses to retint. High-contrast resources still
        // win automatically, so only the Light/Dark axis is set here.
        Surface.RequestedTheme = ThemeHelpers.GetAppTheme() == AppTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        if (!isActive)
        {
            _active = false;
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

        CharactersList.SelectedIndex = _selectedIndex;
        ViewModel.Description = (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
            ? _powerAccent.CharacterDescriptions[_selectedIndex]
            : string.Empty;

        // Size to a content-hugging one-row accent bar and show on-screen (the window is already
        // always-on-top). No content measurement / off-screen probe: the width is computed from the
        // item count, the ListView scrolls if it overflows, and we bring the selected glyph into view
        // once its containers realize.
        SizeAndPosition();
        Show();

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_active && _selectedIndex >= 0 && _selectedIndex < CharactersList.Items.Count)
            {
                CharactersList.ScrollIntoView(CharactersList.Items[_selectedIndex]);
            }
        });

        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Core.Telemetry.PowerAccentShowAccentMenuEvent());
    }

    private void PowerAccent_OnSelectCharacter(int index, string character)
    {
        _selectedIndex = index;
        CharactersList.SelectedIndex = index;

        if (index >= 0 && index < _powerAccent.CharacterDescriptions.Length)
        {
            ViewModel.Description = _powerAccent.CharacterDescriptions[index];
        }

        if (index >= 0 && index < CharactersList.Items.Count)
        {
            CharactersList.ScrollIntoView(CharactersList.Items[index]);
        }
    }

    private void SizeAndPosition()
    {
        // Width hugs the content (like the WPF original's SizeToContent) instead of always filling the
        // monitor: compute it deterministically from the item count - each accent cell is ItemWidthDip
        // wide - rather than measuring the ListView (which is racy while its containers realize). Cap
        // at the monitor's max usable width so a long accent list scrolls horizontally on screen.
        double maxWidthDip = _powerAccent.GetDisplayMaxWidth();
        double contentWidthDip = ViewModel.Characters.Count * ItemWidthDip;

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
        _themeListener?.Dispose();
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
        GC.SuppressFinalize(this);
    }
}
