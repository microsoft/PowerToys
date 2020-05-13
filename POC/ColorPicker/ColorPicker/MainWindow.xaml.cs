using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ColorPicker.ColorPickingFunctionality;
using ColorPicker.ColorPickingFunctionality.SystemEvents;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isColorSelectionEnabled = true;
        private DispatcherTimer _updateTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            _updateTimer.Tick += UpdateCurrentColor;
            _updateTimer.Interval = new TimeSpan(1000);
            _updateTimer.Start();
        }

        private void UpdateCurrentColor(object sender, EventArgs e)
        {
            if (_isColorSelectionEnabled)
            {
                SetColor(PixelColorFinder.GetColorUnderCursor());
            }
        }

        // TODO: Replace this with mouse down (needs transparent overlay window)
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                ToggleColorSelectionMode();
            }
        }

        private void SetColor(Color color)
        {
            ColorPreviewRectangle.Fill = new SolidColorBrush(color);
            RgbTextBox.Text = $"{color.R}, {color.G}, {color.B}";
            HexTextBox.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private void ToggleColorSelectionMode()
        {
            _isColorSelectionEnabled = !_isColorSelectionEnabled;
            if (_isColorSelectionEnabled)
            {
                _updateTimer.Start();
            }
            else
            {
                _updateTimer.Stop();
            }
        }
    }
}
