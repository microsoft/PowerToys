using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PowerOCR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isShiftDown;
        private System.Windows.Point clickedPoint;
        private System.Windows.Point shiftPoint;

        private bool IsSelecting { get; set; }
        private Border selectBorder = new();
        private DpiScale? dpiScale;

        private System.Windows.Point GetMousePos() => PointToScreen(Mouse.GetPosition(this));

        double selectLeft;
        double selectTop;

        double xShiftDelta;
        double yShiftDelta;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
            FullWindow.Rect = new System.Windows.Rect(0, 0, Width, Height);
            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;

            BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
            BackgroundBrush.Opacity = 0.2;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftShift:
                    isShiftDown = false;
                    clickedPoint = new System.Windows.Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                    break;
                case Key.RightShift:
                    isShiftDown = false;
                    clickedPoint = new System.Windows.Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                    break;
                default:
                    break;
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    // WindowUtilities.CloseAllFullscreenGrabs();
                    Close();
                    break;
                default:
                    break;
            }
        }

        private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
                return;

            IsSelecting = true;
            RegionClickCanvas.CaptureMouse();
            // CursorClipper.ClipCursor(this);
            clickedPoint = e.GetPosition(this);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            dpiScale = VisualTreeHelper.GetDpi(this);

            try { RegionClickCanvas.Children.Remove(selectBorder); } catch (Exception) { }

            selectBorder.BorderThickness = new Thickness(2);
            System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 40, 118, 126);
            selectBorder.BorderBrush = new SolidColorBrush(borderColor);
            _ = RegionClickCanvas.Children.Add(selectBorder);
            Canvas.SetLeft(selectBorder, clickedPoint.X);
            Canvas.SetTop(selectBorder, clickedPoint.Y);

            // var screens = System.Windows.Forms.Screen.AllScreens;
            System.Drawing.Point formsPoint = new System.Drawing.Point((int)clickedPoint.X, (int)clickedPoint.Y);
        }

        private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsSelecting)
                return;

            System.Windows.Point movingPoint = e.GetPosition(this);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (!isShiftDown)
                {
                    shiftPoint = movingPoint;
                    selectLeft = Canvas.GetLeft(selectBorder);
                    selectTop = Canvas.GetTop(selectBorder);
                }

                isShiftDown = true;
                xShiftDelta = (movingPoint.X - shiftPoint.X);
                yShiftDelta = (movingPoint.Y - shiftPoint.Y);

                double leftValue = selectLeft + xShiftDelta;
                double topValue = selectTop + yShiftDelta;

                clippingGeometry.Rect = new Rect(
                    new System.Windows.Point(leftValue, topValue),
                    new System.Windows.Size(selectBorder.Width - 2, selectBorder.Height - 2));
                Canvas.SetLeft(selectBorder, leftValue - 1);
                Canvas.SetTop(selectBorder, topValue - 1);
                return;
            }

            isShiftDown = false;

            double left = Math.Min(clickedPoint.X, movingPoint.X);
            double top = Math.Min(clickedPoint.Y, movingPoint.Y);

            selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
            selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
            selectBorder.Height = selectBorder.Height + 2;
            selectBorder.Width = selectBorder.Width + 2;

            clippingGeometry.Rect = new Rect(
                new System.Windows.Point(left, top),
                new System.Windows.Size(selectBorder.Width - 2, selectBorder.Height - 2));
            Canvas.SetLeft(selectBorder, left - 1);
            Canvas.SetTop(selectBorder, top - 1);
        }

        private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSelecting == false)
                return;

            IsSelecting = false;
            // currentScreen = null;
            // CursorClipper.UnClipCursor();
            RegionClickCanvas.ReleaseMouseCapture();
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

            System.Windows.Point mPt = GetMousePos();
            System.Windows.Point movingPoint = e.GetPosition(this);
            movingPoint.X *= m.M11;
            movingPoint.Y *= m.M22;

            movingPoint.X = Math.Round(movingPoint.X);
            movingPoint.Y = Math.Round(movingPoint.Y);

            if (mPt == movingPoint)
                Debug.WriteLine("Probably on Screen 1");

            double correctedLeft = Left;
            double correctedTop = Top;

            if (correctedLeft < 0)
                correctedLeft = 0;

            if (correctedTop < 0)
                correctedTop = 0;

            double xDimScaled = Canvas.GetLeft(selectBorder) * m.M11;
            double yDimScaled = Canvas.GetTop(selectBorder) * m.M22;

            System.Drawing.Rectangle regionScaled = new System.Drawing.Rectangle(
                (int)xDimScaled,
                (int)yDimScaled,
                (int)(selectBorder.Width * m.M11),
                (int)(selectBorder.Height * m.M22));

            string grabbedText = "";

            try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }

            if (regionScaled.Width < 3 || regionScaled.Height < 3)
            {
                BackgroundBrush.Opacity = 0;
                grabbedText = await ImageMethods.GetClickedWord(this, new System.Windows.Point(xDimScaled, yDimScaled));
            }
            else
                grabbedText = await ImageMethods.GetRegionsText(this, regionScaled);

            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                Clipboard.SetText(grabbedText);
                Close();
                // WindowUtilities.CloseAllFullscreenGrabs();
            }
        }

        private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
