using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System;
using System.Windows.Controls;
using System.Windows.Threading;
using ColorPicker.ColorPickingFunctionality;
using System.Windows.Media;
using System.Windows.Interop;

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {
        private const int MAGNIFICATION_WIDTH = 10;
        private const int MAGNIFICATION_HEIGHT = 10;

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

        public void HandleMouseDown(object sender, EventArgs e)
        {
            _broker.ActionTriggered(ActionBroker.ActionTypes.Click, sender, e);
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

        public void OnLoad(object sender, RoutedEventArgs e)
        {
            RefreshMagnifyBox(null, null);
            MouseFollower.Visibility = Visibility.Visible;
        }
        
        public void AddActionCallback(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            _broker.AddCallback(action, callback);
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
            RealignMagnifyBox(e);
        }

        private void RealignMagnifyBox(System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);
            int pX = (int)position.X;
            int pY = (int)position.Y;

            Canvas.SetLeft(MouseFollower, pX + MAGNIFICATION_WIDTH / 2 + 10);
            Canvas.SetTop(MouseFollower, pY + MAGNIFICATION_HEIGHT / 2 + 10);
        }

        private void RefreshMagnifyBox(object sender, EventArgs e)
        {
            System.Drawing.Point cursorPosition = PixelColorFinder.SafeGetCursorPos();

            System.Drawing.Point logicalCaptureCoordsTopLeft = new System.Drawing.Point();
            logicalCaptureCoordsTopLeft.X = cursorPosition.X - MAGNIFICATION_WIDTH / 2;
            logicalCaptureCoordsTopLeft.Y = cursorPosition.Y - MAGNIFICATION_HEIGHT / 2;

            MagnifyImage.Source = ScreenMagnification.GetMagnificationImage(
                logicalCaptureCoordsTopLeft,
                MAGNIFICATION_WIDTH,
                MAGNIFICATION_HEIGHT
            );
        }

        private void ConfigureUpdateTimer()
        {
            _updateTimer.Tick += RefreshMagnifyBox;
            _updateTimer.Interval = new TimeSpan(1000);
        }
    }
}

