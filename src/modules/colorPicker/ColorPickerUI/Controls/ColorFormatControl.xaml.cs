// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;

using ColorPicker.Helpers;
using ColorPicker.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorFormatControl.xaml
    /// </summary>
    public sealed partial class ColorFormatControl : UserControl
    {
        public static readonly DependencyProperty ColorFormatModelProperty = DependencyProperty.Register(nameof(ColorFormatModel), typeof(ColorFormatModel), typeof(ColorFormatControl), new PropertyMetadata(null, ColorFormatModelPropertyChanged));

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorFormatControl), new PropertyMetadata(default(Color), SelectedColorPropertyChanged));

        public static readonly DependencyProperty SelectedColorCopyHelperTextProperty = DependencyProperty.Register(nameof(SelectedColorCopyHelperText), typeof(string), typeof(ColorFormatControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ColorCopiedNotificationBorderProperty = DependencyProperty.Register(nameof(ColorCopiedNotificationBorder), typeof(FrameworkElement), typeof(ColorFormatControl), new PropertyMetadata(null));

        private const int CopyIndicatorStayTimeInMs = 3000;
        private readonly IThrottledActionInvoker _actionInvoker;
        private bool _copyIndicatorVisible;

        public ColorFormatControl()
        {
            InitializeComponent();
            _actionInvoker = App.GetService<IThrottledActionInvoker>();
            CopyToClipboardButton.Click += CopyToClipboardButton_Click;

            // AutomationProperties.Name/ToolTip used WPF's {x:Static p:Resources.*}; resolve the
            // localized strings through the resource loader instead.
            AutomationProperties.SetName(ColorTextRepresentationTextBlock, ResourceLoaderInstance.GetString("Color_Code"));
            AutomationProperties.SetName(CopyToClipboardButton, ResourceLoaderInstance.GetString("Copy_to_clipboard"));
            ToolTipService.SetToolTip(CopyToClipboardButton, ResourceLoaderInstance.GetString("Copy_to_clipboard"));
        }

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public ColorFormatModel ColorFormatModel
        {
            get => (ColorFormatModel)GetValue(ColorFormatModelProperty);
            set => SetValue(ColorFormatModelProperty, value);
        }

        public FrameworkElement ColorCopiedNotificationBorder
        {
            get => (FrameworkElement)GetValue(ColorCopiedNotificationBorderProperty);
            set => SetValue(ColorCopiedNotificationBorderProperty, value);
        }

        public string SelectedColorCopyHelperText
        {
            get => (string)GetValue(SelectedColorCopyHelperTextProperty);
            set => SetValue(SelectedColorCopyHelperTextProperty, value);
        }

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            ClipboardHelper.CopyToClipboard(ColorTextRepresentationTextBlock.Text);
            SessionEventHelper.Event.EditorColorCopiedToClipboard = true;
            if (!_copyIndicatorVisible)
            {
                AppearCopiedIndicator();
            }

            _actionInvoker.ScheduleAction(() => HideCopiedIndicator(), CopyIndicatorStayTimeInMs);
        }

        private static void SelectedColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ColorFormatControl)d;
            if (self.ColorFormatModel == null)
            {
                return;
            }

            var colorText = self.ColorFormatModel.GetColorText((Color)e.NewValue);
            self.ColorTextRepresentationTextBlock.Text = colorText;
            ToolTipService.SetToolTip(self.ColorTextRepresentationTextBlock, colorText);
            self.SelectedColorCopyHelperText = string.Format(CultureInfo.InvariantCulture, "{0} {1}", self.ColorFormatModel.FormatName, colorText);

            // WPF used a RelativeSource FindAncestor binding for this; WinUI {Binding} has no
            // FindAncestor, so push the help text onto the button directly.
            AutomationProperties.SetHelpText(self.CopyToClipboardButton, self.SelectedColorCopyHelperText);
        }

        private static void ColorFormatModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ColorFormatControl)d;
            var model = (ColorFormatModel)e.NewValue;
            self.FormatNameTextBlock.Text = model.FormatName;
            ToolTipService.SetToolTip(self.FormatNameTextBlock, model.FormatName);
        }

        private void AppearCopiedIndicator()
        {
            _copyIndicatorVisible = true;
            AnimateCopiedIndicator(1.0, 56);

            if (ColorCopiedNotificationBorder is not Border border || border.Child is not StackPanel panel)
            {
                return;
            }

            var innerTextBlock = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (innerTextBlock == null)
            {
                return;
            }

            var peer = FrameworkElementAutomationPeer.FromElement(innerTextBlock)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(innerTextBlock);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        private void HideCopiedIndicator()
        {
            _copyIndicatorVisible = false;
            AnimateCopiedIndicator(0, 0);
        }

        private void AnimateCopiedIndicator(double opacity, double height)
        {
            if (ColorCopiedNotificationBorder == null)
            {
                return;
            }

            var storyboard = new Storyboard();

            var opacityAnimation = new DoubleAnimation
            {
                To = opacity,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(opacityAnimation, ColorCopiedNotificationBorder);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            storyboard.Children.Add(opacityAnimation);

            var heightAnimation = new DoubleAnimation
            {
                To = height,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),

                // Height is a layout property, so the animation must opt in to dependent animation.
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(heightAnimation, ColorCopiedNotificationBorder);
            Storyboard.SetTargetProperty(heightAnimation, "Height");
            storyboard.Children.Add(heightAnimation);

            storyboard.Begin();
        }
    }
}
