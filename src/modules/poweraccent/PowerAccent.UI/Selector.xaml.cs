// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.WinUI.Animations;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Flyout;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using CoreSize = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class Selector : TransparentWindow, IDisposable
{
    // Deterministic accent-bar geometry (DIP). We deliberately do NOT measure the ListView to
    // size the window: a ListView wraps its items in a ScrollViewer (whose DesiredSize does not
    // reflect content size), and measuring it before/while its item containers realize is racy
    // (it intermittently reports 0, yielding a blank/clipped bar). Instead the popup is a fixed
    // one-row bar up to the monitor's max width; the ListView scrolls horizontally and
    // ScrollIntoView reveals the selected glyph.
    private const double RowHeightDip = 52;          // one row of accent pills (item Height=48 + card border)
    private const double DescriptionHeightDip = 48;  // extra row shown when the Unicode description is on

    private readonly Core.PowerAccent _powerAccent;
    private int _selectedIndex = -1;
    private bool _active;

    public SelectorViewModel ViewModel { get; } = new();

    public Selector()
    {
        InitializeComponent();

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

        // No animations: instant show/hide for typing-aid responsiveness.
        ShowAnimations = new ImplicitAnimationSet();
        HideAnimations = new ImplicitAnimationSet();

        _powerAccent = new Core.PowerAccent(action => DispatcherQueue.TryEnqueue(() => action()));
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectCharacter;
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

        ViewModel.SelectedIndex = _selectedIndex;
        CharactersList.SelectedIndex = _selectedIndex;
        ViewModel.Description = (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
            ? _powerAccent.CharacterDescriptions[_selectedIndex]
            : string.Empty;

        // Size to a deterministic one-row accent bar and show on-screen (the window is already
        // always-on-top). No content measurement / off-screen probe: the ListView scrolls and we
        // bring the selected glyph into view once its containers realize.
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
        ViewModel.SelectedIndex = index;
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
        // Deterministic accent-bar size (DIP): full monitor max width, one row (+ description row).
        double widthDip = _powerAccent.GetDisplayMaxWidth();
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
