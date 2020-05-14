using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

using System;
using System.Windows.Controls;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using System.Threading.Tasks;
using ColorPicker.ColorPickingFunctionality;

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
            SetWindowSizeToCoverAllScreens();
            ConfigureUpdateTimer();
        }

        private void SetWindowSizeToCoverAllScreens()
        {

            DetermineAndSetWindowHeight();
            DetermineAndSetWindowWidth();
        }

        private void DetermineAndSetWindowHeight()
        {
            int minY = 0;
            int maxY = 0;

            foreach (Screen s in Screen.AllScreens)
            {
                minY = Math.Min(minY, s.Bounds.Y);
                maxY = Math.Max(maxY, s.Bounds.Y + s.Bounds.Height);
            }

            MinHeight = maxY - minY;
            Top = minY;
        }

        private void DetermineAndSetWindowWidth()
        {
            int minX = 0;
            int maxX = 0;

            foreach (Screen s in Screen.AllScreens)
            {
                Debug.WriteLine(s.Bounds);
                minX = Math.Min(minX, s.Bounds.X);
                maxX = Math.Max(maxX, s.Bounds.X + s.Bounds.Width);
            }
            Debug.WriteLine(maxX - minX);
            MinWidth = maxX - minX;
            Left = minX;
            Debug.WriteLine(Width);
        }

        public void HandleMouseDown(object sender, EventArgs e)
        {
            Debug.WriteLine(Width);
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

        internal void AddActionCallBack(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            _broker.AddCallback(action, callback);
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

            int logicalCoordForMagnificationLeft = cursorPosition.X - MAGNIFICATION_WIDTH / 2;
            int logicalCoordForMagnificationTop = cursorPosition.Y - MAGNIFICATION_HEIGHT / 2;

            MagnifyImage.Source = ScreenMagnification.GetMagnificationImage(logicalCoordForMagnificationLeft, logicalCoordForMagnificationTop, MAGNIFICATION_WIDTH, MAGNIFICATION_HEIGHT);
        }

        private void ConfigureUpdateTimer()
        {
            _updateTimer.Tick += RefreshMagnifyBox;
            _updateTimer.Interval = new TimeSpan(1000);
        }
    }
}

