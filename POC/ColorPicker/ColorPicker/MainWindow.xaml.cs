using System;
using System.ComponentModel;
using System.Windows;
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

        protected override void OnClosing(CancelEventArgs e)
        {
            _transparentWindow.Close();
            base.OnClosing(e);
        }

        private void ConfigureTransparentWindow()
        {
            _transparentWindow.AddActionCallback(ActionBroker.ActionTypes.Click, OnTransparentScreenClick);
        }

        private void ConfigureUpdateTimer()
        {
            _updateTimer.Tick += UpdateCurrentColor;
            _updateTimer.Interval = new TimeSpan(1000);
        }

        private void OnTransparentScreenClick(object sender, EventArgs e)
        {
            SetColor(PixelColorFinder.GetColorUnderCursor());
            DeactivateColorSelectionMode();
        }

        private void OnNewColorButtonClick(object sender, EventArgs e)
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
    }
}
