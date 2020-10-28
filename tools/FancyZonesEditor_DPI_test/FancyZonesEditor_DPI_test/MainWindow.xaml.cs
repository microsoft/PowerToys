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
                var resolution = String.Format("(L:{0,5}, T:{1,3}, W:{2,4}, H:{3,4})", Resolution.Left, Resolution.Top, Resolution.Width, Resolution.Height);
                var workArea = String.Format("(L:{0,5}, T:{1,3}, W:{2,4}, H:{3,4})", WorkArea.Left, WorkArea.Top, WorkArea.Width, WorkArea.Height);

                return "Window DPI: " + WindowDPI + ", Monitor DPI: " + MonitorDPI +
                    ", Resolution: " + resolution + ", WorkArea: " + workArea;
            }
        }

        private List<OverlayWindow> workAreaWindows;

        public MainWindow()
        {
            InitializeComponent();

            var colors = new Brush[] { Brushes.Yellow, Brushes.Orange, Brushes.OrangeRed };

            var screens = System.Windows.Forms.Screen.AllScreens;
            List<ScreenInfo> items = new List<ScreenInfo>();

            workAreaWindows = new List<OverlayWindow>();

            var monitors = MonitorsInfo.GetMonitors();

            for (int i = 0; i < screens.Length; i++)
            {
                var monitor = monitors[i];
                ScreenInfo screenInfo = new ScreenInfo();
                var window = new OverlayWindow();
                window.Opacity = 0.5;
                window.Background = colors[i % colors.Length];

                // resolution
                screenInfo.Resolution = new Rect(monitor.MonitorInfo.monitor.left, monitor.MonitorInfo.monitor.top, 
                    monitor.MonitorInfo.monitor.width, monitor.MonitorInfo.monitor.height);

                // work area
                var workArea = DpiAwareness.DeviceToLogicalRect(window, new Rect(monitor.MonitorInfo.work.left, monitor.MonitorInfo.work.top,
                    monitor.MonitorInfo.work.width, monitor.MonitorInfo.work.height));
                screenInfo.WorkArea = workArea;

                // dpi
                bool en = DpiAwareness.IsPerMonitorAwarenessEnabled;

                screenInfo.WindowDPI = window.GetDpiX();

                // open window
                window.Left = workArea.X;
                window.Top = workArea.Y;
                window.Width = workArea.Width;
                window.Height = workArea.Height;

                workAreaWindows.Add(window);
                window.Show();

                // get monitor dpi
                double dpiX = 0, dpiY = 0;
                DpiAwareness.GetMonitorDpi(monitors[i].MonitorHandle, out dpiX, out dpiY);

                screenInfo.MonitorDPI = (int)dpiX;

                items.Add(screenInfo);
            }

            MonitorList.ItemsSource = items;
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
