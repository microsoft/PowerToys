using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Threading;
using ColorPicker.ColorPickingFunctionality;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Input;

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {
        private const int MAGNIFICATION_WIDTH = 9;
        private const int MAGNIFICATION_HEIGHT = 9;

        private ActionBroker _broker = new ActionBroker();
        private DispatcherTimer _updateTimer = new DispatcherTimer();

        public TransparentWindow()
        {
            InitializeComponent();
            this.Cursor = System.Windows.Input.Cursors.Cross;

            ConfigureUpdateTimer();

            Width = SystemParameters.VirtualScreenWidth;
            Left = SystemParameters.VirtualScreenLeft;
            Height = SystemParameters.VirtualScreenHeight;
            Top = SystemParameters.VirtualScreenTop;
        }

        public new void Show()
        {
            _updateTimer.Start();
            base.Show();
        }

        public new void Hide()
        {
            _updateTimer.Stop();
            base.Hide();
        }

        public void AddActionCallback(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            _broker.AddCallback(action, callback);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshMagnificationBox(null, null);
            MouseFollower.Visibility = Visibility.Visible;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _broker.ActionTriggered(ActionBroker.ActionTypes.Click, sender, e);
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _broker.ActionTriggered(ActionBroker.ActionTypes.Escape, sender, e);
            }
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RealignMagnificationBox(e);
        }

        private void ConfigureUpdateTimer()
        {
            _updateTimer.Tick += RefreshMagnificationBox;
            _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        }

        private void RefreshMagnificationBox(object sender, EventArgs e)
        {
            System.Drawing.Point cursorPosition = PixelColorFinder.SafeGetCursorPos();

            System.Drawing.Point captureCoordsTopLeft = new System.Drawing.Point(cursorPosition.X - MAGNIFICATION_WIDTH / 2,
                                                                                 cursorPosition.Y - MAGNIFICATION_HEIGHT / 2);

            MagnifiedImage.Source = ScreenMagnification.GetMagnificationImage(captureCoordsTopLeft,
                                                                              MAGNIFICATION_WIDTH,
                                                                              MAGNIFICATION_HEIGHT);
        }

        private void RealignMagnificationBox(System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);

            const int LAG_OFFSET = 10;
            Canvas.SetLeft(MouseFollower, position.X + MAGNIFICATION_WIDTH / 2 + LAG_OFFSET);
            Canvas.SetTop(MouseFollower, position.Y + MAGNIFICATION_HEIGHT / 2 + LAG_OFFSET);
        }
    }
}

