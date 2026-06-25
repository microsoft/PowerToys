// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.WinUI.Animations;
using Microsoft.PowerToys.Common.UI.Controls.Flyout;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using CorePoint = PowerAccent.Core.Point;
using CoreSize = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class Selector : TransparentWindow, IDisposable
{
    private readonly Core.PowerAccent _powerAccent;
    private int _selectedIndex = -1;

    public SelectorViewModel ViewModel { get; } = new();

    public Selector()
    {
        InitializeComponent();

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
            Hide();
            ViewModel.Characters.Clear();
            _selectedIndex = -1;
            return;
        }

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

        SizeAndPosition();
        Show();

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
        double maxWidthDip = _powerAccent.GetDisplayMaxWidth();

        // Measure the realized card content to get the natural popup size (DIP).
        RootContent.UpdateLayout();
        RootContent.Measure(new Size(maxWidthDip, double.PositiveInfinity));
        var desired = RootContent.DesiredSize;

        // Calculation works in physical pixels; it multiplies the DIP size by the active
        // monitor's DPI internally and returns the physical top-left for the chosen anchor.
        var coordinates = _powerAccent.GetDisplayCoordinates(new CoreSize(desired.Width, desired.Height));

        var display = DisplayArea.GetFromPoint(
            new PointInt32((int)Math.Round(coordinates.X), (int)Math.Round(coordinates.Y)),
            DisplayAreaFallback.Nearest);

        double dpiScale = FlyoutWindowHelper.GetDpiScale(display);
        int widthPhysical = (int)Math.Ceiling(desired.Width * dpiScale);
        int heightPhysical = (int)Math.Ceiling(desired.Height * dpiScale);

        var rect = new RectInt32(
            (int)Math.Round(coordinates.X),
            (int)Math.Round(coordinates.Y),
            widthPhysical,
            heightPhysical);

        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, display, rect);
    }

    public void Dispose()
    {
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
    }
}
