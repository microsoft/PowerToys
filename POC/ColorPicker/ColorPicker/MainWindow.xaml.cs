using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

using ColorPicker.ColorPickingFunctionality;
using ColorPicker.ColorPickingFunctionality.SystemEvents;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RegisterdMouseEventHook mouseEvent = new RegisterdMouseEventHook(PixelColorFinder.HandleMouseClick);
        private TransparentWindowController transparentWindow = new TransparentWindowController();

        public MainWindow()
        {
            InitializeComponent();
            TransparentWindow w = new TransparentWindow();
            w.Show();
            //cursor.SetCursorToCrossOutsideCurrentWindow();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Debug.WriteLine("running");
            transparentWindow.Close();
            base.OnClosing(e);
        }
    }
}
