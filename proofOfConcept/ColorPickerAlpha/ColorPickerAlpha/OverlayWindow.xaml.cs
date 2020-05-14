using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorPickerAlpha
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        MainWindow mainWnd;

        public OverlayWindow(MainWindow mainWnd)
        {
            InitializeComponent();

            this.mainWnd = mainWnd;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = (Brush) new BrushConverter().ConvertFrom("#01000000"); 
            SetSize();

            Loaded += delegate
            {
                MouseDown += Mouse_Click;
                Activated += delegate { Topmost = true; };
                Deactivated += delegate { Topmost = true; };
            };
        }

        private void Mouse_Click(object sender, RoutedEventArgs e)
        {
            if (!mainWnd.pickerActive || mainWnd.isInWindow)
                return;

            mainWnd.CopyToClipboard();
            mainWnd.pickerActive = false;
        }

        private void SetSize()
        {
            Height = SystemParameters.VirtualScreenHeight;
            Width = SystemParameters.VirtualScreenWidth;
            Top = 0;
            Left = 0;
        }
    }
}
