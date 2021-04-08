// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ColorPicker.Helpers;
using ColorPicker.Models;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorFormatControl.xaml
    /// </summary>
    public partial class ColorFormatControl : UserControl
    {
        public static readonly DependencyProperty ColorFormatModelProperty = DependencyProperty.Register("ColorFormatModel", typeof(ColorFormatModel), typeof(ColorFormatControl), new PropertyMetadata(ColorFormatModelPropertyChanged));

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorFormatControl), new PropertyMetadata(SelectedColorPropertyChanged));

        public static readonly DependencyProperty ColorCopiedNotificationBorderProperty = DependencyProperty.Register("ColorCopiedNotificationBorder", typeof(FrameworkElement), typeof(ColorFormatControl), new PropertyMetadata(ColorCopiedBorderPropertyChanged));

        private const int CopyIndicatorStayTimeInMs = 3000;
        private IThrottledActionInvoker _actionInvoker;
        private bool _copyIndicatorVisible;

        public Color SelectedColor
        {
            get { return (Color)GetValue(SelectedColorProperty); }
            set { SetValue(SelectedColorProperty, value); }
        }

        public ColorFormatModel ColorFormatModel
        {
            get { return (ColorFormatModel)GetValue(ColorFormatModelProperty); }
            set { SetValue(ColorFormatModelProperty, value); }
        }

        public FrameworkElement ColorCopiedNotificationBorder
        {
            get { return (FrameworkElement)GetValue(ColorCopiedNotificationBorderProperty); }
            set { SetValue(ColorCopiedNotificationBorderProperty, value); }
        }

        public ColorFormatControl()
        {
            InitializeComponent();
            _actionInvoker = Bootstrapper.Container.GetExportedValue<IThrottledActionInvoker>();
            CopyToClipboardButton.Click += CopyToClipboardButton_Click;
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
            ((ColorFormatControl)d).ColorTextRepresentationTextBlock.Text = ((ColorFormatControl)d).ColorFormatModel.Convert((Color)e.NewValue);
        }

        private static void ColorFormatModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorFormatControl)d).FormatNameTextBlock.Text = ((ColorFormatModel)e.NewValue).FormatName;
        }

        private static void ColorCopiedBorderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorFormatControl)d).ColorCopiedNotificationBorder = (FrameworkElement)e.NewValue;
        }

        private void AppearCopiedIndicator()
        {
            _copyIndicatorVisible = true;
            var opacityAppear = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(300)));
            opacityAppear.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            var resize = new DoubleAnimation(56, new Duration(TimeSpan.FromMilliseconds(300)));

            resize.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            ColorCopiedNotificationBorder.BeginAnimation(Border.OpacityProperty, opacityAppear);
            ColorCopiedNotificationBorder.BeginAnimation(Border.HeightProperty, resize);
        }

        private void HideCopiedIndicator()
        {
            _copyIndicatorVisible = false;
            var opacityDisappear = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(300)));
            opacityDisappear.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            var resize = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(300)));

            resize.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            ColorCopiedNotificationBorder.BeginAnimation(Border.OpacityProperty, opacityDisappear);
            ColorCopiedNotificationBorder.BeginAnimation(Border.HeightProperty, resize);
        }
    }
}
