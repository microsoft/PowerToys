// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using ShortcutGuide.Controls;
using ShortcutGuide.Helpers;
using Windows.Foundation;
using WinRT.Interop;
using WinUIEx;
using static ShortcutGuide.NativeMethods;

namespace ShortcutGuide.ShortcutGuideXAML
{
    public sealed partial class TaskbarWindow : TransparentWindow
    {
        private const int SlideInDurationMs = 240;
        private const int SlideOutDurationMs = 180;

        // Card padding inside the transparent window so the acrylic chrome's
        // shadow / corners have room to render. Matches MainWindow.
        private const double CardMargin = 12;

        // Fallback animation offset until the first measurement-based attach runs.
        private const double DefaultSlideOffset = 90;

        private float DPI => DpiHelper.GetDPIScaleForWindow(WindowNative.GetWindowHandle(this));

        private Rect WorkArea => DisplayHelper.GetWorkAreaForDisplayWithWindow(WindowNative.GetWindowHandle(this));

        public TaskbarWindow()
        {
            this.InitializeComponent();

            this.Card.Margin = new Thickness(CardMargin);

            AttachSlideAnimations(DefaultSlideOffset);

            this.UpdateTasklistButtons();
            this.Activated += (_, _) => this.UpdateTasklistButtons();
        }

        private void AttachSlideAnimations(double offsetDips)
        {
            // Slide up from below — the numbers window sits just above the
            // taskbar, so a vertical slide from the bottom edge reads best.
            var offsetString = $"0,{offsetDips.ToString(CultureInfo.InvariantCulture)},0";

            var showAnimations = new ImplicitAnimationSet
            {
                new OpacityAnimation
                {
                    From = 0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(SlideInDurationMs),
                    EasingMode = EasingMode.EaseOut,
                    EasingType = EasingType.Cubic,
                },
                new TranslationAnimation
                {
                    From = offsetString,
                    To = "0,0,0",
                    Duration = TimeSpan.FromMilliseconds(SlideInDurationMs),
                    EasingMode = EasingMode.EaseOut,
                    EasingType = EasingType.Cubic,
                },
            };

            var hideAnimations = new ImplicitAnimationSet
            {
                new OpacityAnimation
                {
                    From = 1.0,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(SlideOutDurationMs),
                    EasingMode = EasingMode.EaseIn,
                    EasingType = EasingType.Cubic,
                },
                new TranslationAnimation
                {
                    From = "0,0,0",
                    To = offsetString,
                    Duration = TimeSpan.FromMilliseconds(SlideOutDurationMs),
                    EasingMode = EasingMode.EaseIn,
                    EasingType = EasingType.Cubic,
                },
            };

            Implicit.SetShowAnimations(this.Card, showAnimations);
            Implicit.SetHideAnimations(this.Card, hideAnimations);
        }

        public void UpdateTasklistButtons()
        {
            // This move ensures the window spawns on the same monitor as the main window
            AppWindow.MoveInZOrderAtBottom();
            AppWindow.Move(App.MainWindow.AppWindow.Position);
            TasklistButton[] buttons = [];
            try
            {
                buttons = TasklistPositions.GetButtons();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enumerate taskbar buttons via TasklistPositions.GetButtons.", ex);
            }

            if (buttons.Length == 0)
            {
                AppWindow.Hide();
                return;
            }

            float dpi = this.DPI;
            double windowsLogoColumnWidth = this.WindowsLogoColumnWidth.Width.Value;
            double contentHeight = 58;
            double windowHeight = contentHeight + (2 * CardMargin);
            double windowMargin = CardMargin * dpi;
            double windowWidth = windowsLogoColumnWidth;
            double xPosition = buttons[0].X - (windowsLogoColumnWidth * dpi);
            double yPosition = this.WorkArea.Bottom - (windowHeight * dpi);

            this.KeyHolder.Children.Clear();

            foreach (TasklistButton b in buttons)
            {
                TaskbarIndicator indicator = new()
                {
                    Label = b.Keynum >= 10 ? "0" : b.Keynum.ToString(CultureInfo.InvariantCulture),
                    Height = b.Height / dpi,
                    Width = b.Width / dpi,
                };

                windowWidth += indicator.Width;

                this.KeyHolder.Children.Add(indicator);

                double indicatorPos = (b.X - xPosition) / dpi;
                Canvas.SetLeft(indicator, indicatorPos - windowsLogoColumnWidth);
            }

            this.MoveAndResize(xPosition - windowMargin, yPosition, windowWidth + (2 * windowMargin), windowHeight);
            AppWindow.MoveInZOrderAtTop();

            // Re-tune the slide distance to the actual final window height so
            // the card glides up from just below the visible bottom edge.
            AttachSlideAnimations(windowHeight);
        }
    }
}
