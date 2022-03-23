using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PeekUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnShowPeek, DispatcherQueue);
        }

        private void OnShowPeek()
        {
            Activate();
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";

            // Retrieve the window handle (HWND) of the current (XAML) WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Retrieve the WindowId that corresponds to hWnd.
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

            // Lastly, retrieve the AppWindow for the current (XAML) WinUI 3 window.
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                appWindow.Hide();
            }

        }
    }
}
