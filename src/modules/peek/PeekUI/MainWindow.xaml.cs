using interop;
using PeekUI.Helpers;
using PeekUI.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace PeekUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel; 

        public MainWindow()
        {
            InitializeComponent();
            IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            var attribute = InteropHelper.DWMWINDOWATTRIBUTE.DWMWAWINDOWCORNERPREFERENCE;
            var preference = InteropHelper.DWMWINDOWCORNERPREFERENCE.DWMWCPROUND;
            InteropHelper.DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));


            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), TogglePeek);

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.MainWindowVisibility = Visibility.Collapsed;
            e.Cancel = true;
        }

        private void TogglePeek()
        {
            // put window in focus and in foreground
            // get file
            // if file valid toggle/update file displayed and bring to foreground

            var selectedItems = FileExplorerHelper.GetSelectedItems();

            if(_viewModel.MainWindowVisibility == Visibility.Visible)
            {

            }
            else
            {

            }

            if (!string.IsNullOrWhiteSpace(selectedItems.FirstOrDefault()))
            {
                _viewModel.MainWindowVisibility = _viewModel.MainWindowVisibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        //private void BringProcessToForeground()
        //{
        //    // Use SendInput hack to allow Activate to work - required to resolve focus issue https://github.com/microsoft/PowerToys/issues/4270
        //    WindowsInteropHelper.INPUT input = new WindowsInteropHelper.INPUT { Type = WindowsInteropHelper.INPUTTYPE.INPUTMOUSE, Data = { } };
        //    WindowsInteropHelper.INPUT[] inputs = new WindowsInteropHelper.INPUT[] { input };

        //    // Send empty mouse event. This makes this thread the last to send input, and hence allows it to pass foreground permission checks
        //    _ = NativeMethods.SendInput(1, inputs, WindowsInteropHelper.INPUT.Size);
        //    Activate();
        //}

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.MainWindowVisibility = Visibility.Collapsed;

            //BringProcessToForeground();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InteropHelper.SetToolWindowStyle(this);
        }
    }
}
