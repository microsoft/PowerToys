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

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {
        private const int MAGNIFICATION_WIDTH = 50;
        private const int MAGNIFICATION_HEIGHT = 50;

        private ActionBroker _broker = new ActionBroker();

        [DllImport("User32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("User32.dll")]
        public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);

        public TransparentWindow()
        {
            InitializeComponent();
            this.Cursor = System.Windows.Input.Cursors.Cross;
            SetWindowSizeToCoverAllScreens();
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

            Height = maxY - minY;
            Top = minY;
        }

        private void DetermineAndSetWindowWidth()
        {
            int minX = 0;
            int maxX = 0;

            foreach (Screen s in Screen.AllScreens)
            {
                minX = Math.Min(minX, s.Bounds.X);
                maxX = Math.Max(maxX, s.Bounds.X + s.Bounds.Width);
            }

            Width = maxX - minX;
            Left = minX;
        }

        public void HandleMouseDown(object sender, EventArgs e)
        {
            _broker.ActionTriggered(ActionBroker.ActionTypes.Click, sender, e);
        }

        internal void AddActionCallBack(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            _broker.AddCallback(action, callback);
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);
            int pX = (int)position.X;
            int pY = (int)position.Y;

            int logicalCoordForMagnificationLeft = pX + (int)Left - MAGNIFICATION_WIDTH / 2;
            int logicalCoordForMagnificationTop = pY + (int)Top - MAGNIFICATION_HEIGHT / 2;


            MagnifyImage.Source = ScreenMagnification.GetMagnificationImage(logicalCoordForMagnificationLeft, logicalCoordForMagnificationTop, MAGNIFICATION_WIDTH, MAGNIFICATION_HEIGHT);
            Canvas.SetLeft(MouseFollower, pX + MAGNIFICATION_WIDTH / 2 + 1);
            Canvas.SetTop(MouseFollower, pY + MAGNIFICATION_HEIGHT / 2 + 1);
        }


    }
}

