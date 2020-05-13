using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private TransparentWindow transparentWindow = new TransparentWindow();
        private bool _isColorSelectionEnabled = true;
        private DispatcherTimer _updateTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            transparentWindow.AddActionCallBack(ActionBroker.ActionTypes.Click, TransparentWindowClick);
            transparentWindow.Show();
            _updateTimer.Tick += UpdateCurrentColor;
            _updateTimer.Interval = new TimeSpan(1000);
            _updateTimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            transparentWindow.Close();
            base.OnClosing(e);

        }

        private void UpdateCurrentColor(object sender, EventArgs e)
        {
            if (_isColorSelectionEnabled)
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

        private void ToggleColorSelectionMode()
        {
            _isColorSelectionEnabled = !_isColorSelectionEnabled;
            if (_isColorSelectionEnabled)
            {
                transparentWindow.Show();
                _updateTimer.Start();
            }
            else
            {
                transparentWindow.Hide();
                _updateTimer.Stop();
            }
        }

        private void TransparentWindowClick(object sender, EventArgs e)
        {
            ToggleColorSelectionMode();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleColorSelectionMode();
        }
    }
}
