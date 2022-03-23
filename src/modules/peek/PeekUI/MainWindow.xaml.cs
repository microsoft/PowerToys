using interop;
using System.Windows;

namespace PeekUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), TogglePeek);
        }

        private void TogglePeek()
        {
            // put window in focus and in foreground
            Visibility = Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
