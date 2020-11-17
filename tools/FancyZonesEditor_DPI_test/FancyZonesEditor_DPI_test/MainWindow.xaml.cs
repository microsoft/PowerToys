using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Utilities;

namespace FancyZonesEditor_DPI_test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        struct ScreenInfo
        {
            public int MonitorDPI { get; set; }

            public double WindowDPI { get; set; }

            public Rect Resolution { get; set; }

            public Rect WorkArea { get; set; }

            public override string ToString()
            {
                var resolution = String.Format("X:{0,5}, Y:{1,5}, W:{2,5}, H:{3,5}", Resolution.Left, Resolution.Top, Resolution.Width, Resolution.Height);
                var workArea = String.Format("X:{0,5}, Y:{1,5}, W:{2,5}, H:{3,5}", WorkArea.Left, WorkArea.Top, WorkArea.Width, WorkArea.Height);

                return "Monitor DPI: " + MonitorDPI + " - Resolution: " + resolution + " - WorkArea: " + workArea;
            }
        }

        private List<OverlayWindow> workAreaWindows;

        public MainWindow()
        {
            InitializeComponent();

            double primaryMonitorDPI = 96f;

            var colors = new Brush[] { Brushes.Green, Brushes.Blue, Brushes.Red };

            var screens = System.Windows.Forms.Screen.AllScreens;
            List<ScreenInfo> screenInfoList = new List<ScreenInfo>();

            workAreaWindows = new List<OverlayWindow>();

            var monitors = MonitorsInfo.GetMonitors();

            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Primary)
                {
                    double monitorDPI;
                    DpiAwareness.GetMonitorDpi(monitors[i].MonitorHandle, out monitorDPI, out monitorDPI);
                    primaryMonitorDPI = monitorDPI;
                    break;
                }
            }

            for (int i = 0; i < screens.Length; i++)
            {
                var monitor = monitors[i];
                ScreenInfo screenInfo = new ScreenInfo();
                var window = new OverlayWindow
                {
                    Opacity = 0.8,
                    Background = colors[i % colors.Length],
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(4, 4, 4, 4)
                };

                // get monitor dpi
                double monitorDPI;
                DpiAwareness.GetMonitorDpi(monitors[i].MonitorHandle, out monitorDPI, out monitorDPI);
                screenInfo.MonitorDPI = (int)monitorDPI;

                // screen resolution
                screenInfo.Resolution = new Rect(monitor.MonitorInfo.monitor.left, monitor.MonitorInfo.monitor.top, 
                    monitor.MonitorInfo.monitor.width, monitor.MonitorInfo.monitor.height);

                // work area
                Rect workedArea = new Rect(monitor.MonitorInfo.work.left, monitor.MonitorInfo.work.top,
                    monitor.MonitorInfo.work.width, monitor.MonitorInfo.work.height);

                double scaleFactor = 96f / primaryMonitorDPI;
                workedArea.X *= scaleFactor;
                workedArea.Y *= scaleFactor;
                workedArea.Width *= scaleFactor;
                workedArea.Height *= scaleFactor;

                screenInfo.WorkArea = workedArea;

                screenInfo.WindowDPI = window.GetDpiX();
                screenInfoList.Add(screenInfo);

                // open window
                window.Left = workedArea.X;
                window.Top = workedArea.Y;
                window.Width = workedArea.Width;
                window.Height = workedArea.Height;

                workAreaWindows.Add(window);
                window.Show();

            }

            MonitorList.ItemsSource = screenInfoList;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (OverlayWindow window in workAreaWindows)
            {
                window.Close();
            }
        }
    }
}
