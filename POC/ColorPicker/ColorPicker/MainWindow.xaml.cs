using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColorPicker.ColorPickingFunctionality;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TransparentWindow _transparentWindow = new TransparentWindow();
        private DispatcherTimer _updateTimer = new DispatcherTimer();
        private Color _previousColor;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureTransparentWindow();
            ConfigureUpdateTimer();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            IconHelper.RemoveIcon(this);
            ActivateColorSelectionMode();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            // Fix to let the pop-ups follow the window if it is dragged around
            // Source: https://stackoverflow.com/questions/5736359/popup-control-moves-with-parent
            RgbCopiedPopup.HorizontalOffset += 1;
            RgbCopiedPopup.HorizontalOffset -= 1;
            HexCopiedPopup.HorizontalOffset += 1;
            HexCopiedPopup.HorizontalOffset -= 1;
            base.OnLocationChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _transparentWindow.Close();
            base.OnClosing(e);
        }

        private void ConfigureTransparentWindow()
        {
            _transparentWindow.AddActionCallback(ActionBroker.ActionTypes.Click, OnTransparentScreenClick);
            _transparentWindow.AddActionCallback(ActionBroker.ActionTypes.Escape, OnTransparentScreenEscape);
        }

        private void ConfigureUpdateTimer()
        {
            _updateTimer.Tick += UpdateCurrentColor;
            _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        }

        private void OnTransparentScreenClick(object sender, EventArgs e)
        {
            SetColor(PixelColorFinder.GetColorUnderCursor());
            DeactivateColorSelectionMode();
        }

        private void OnTransparentScreenEscape(object sender, EventArgs e)
        {
            SetColor(_previousColor);
            DeactivateColorSelectionMode();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _transparentWindow.Visibility == Visibility.Visible)
            {
                OnTransparentScreenEscape(sender, e);
            }
        }

        private void OnNewColorButtonClick(object sender, RoutedEventArgs e)
        {
            if (NewColorButton.IsChecked ?? false)
            {
                ActivateColorSelectionMode();
            }
            else
            {
                DeactivateColorSelectionMode();
            }
        }

        private void OnTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Source: https://www.intertech.com/Blog/how-to-select-all-text-in-a-wpf-textbox-on-focus/
            // and https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
            TextBox textBox = sender as TextBox;
            if (textBox != null && !textBox.IsKeyboardFocusWithin)
            {
                if (e.OriginalSource.GetType().Name == "TextBoxView")
                {
                    e.Handled = true;
                    textBox.Focus();
                }
            }
        }

        private void OnTextBoxFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = e.OriginalSource as TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
                CopyToClipboard(textBox);
            }
        }

        private void UpdateCurrentColor(object sender, EventArgs e)
        {
            if (NewColorButton.IsChecked ?? false)
            {
                SetColor(PixelColorFinder.GetColorUnderCursor());
            }
        }

        private void SetColor(Color color)
        {
            ColorPreviewRectangle.Fill = new SolidColorBrush(color);
            RgbTextBox.Text = $"{color.R}, {color.G}, {color.B}";
            HexTextBox.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private void ActivateColorSelectionMode()
        {
            _previousColor = (ColorPreviewRectangle.Fill as SolidColorBrush).Color;
            NewColorButton.IsChecked = true;
            _transparentWindow.Show();
            _updateTimer.Start();
        }

        private void DeactivateColorSelectionMode()
        {
            NewColorButton.IsChecked = false;
            _transparentWindow.Hide();
            _updateTimer.Stop();
        }

        private void CopyToClipboard(TextBox textBox)
        {
            textBox.Copy();

            Popup popup = textBox == RgbTextBox ? RgbCopiedPopup : HexCopiedPopup;
            popup.IsOpen = true;

            DispatcherTimer popupTimer = new DispatcherTimer();
            popupTimer.Interval = TimeSpan.FromSeconds(3);
            popupTimer.Tick += (object sender, EventArgs e) =>
            {
                popup.IsOpen = false;
                popupTimer.Stop();
            };
            popupTimer.Start();
        }
    }
}
