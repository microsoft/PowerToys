using interop;
using ModernWpf.Controls;
using PeekUI.Extensions;
using PeekUI.Helpers;
using PeekUI.ViewModels;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PeekUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private readonly MainViewModel _viewModel; 

        public MainWindow()
        {
            InitializeComponent();

            //todo: put in method
            IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(GetWindow(this)).EnsureHandle();
            var attribute = InteropHelper.DWMWINDOWATTRIBUTE.DWMWAWINDOWCORNERPREFERENCE;
            var preference = InteropHelper.DWMWINDOWCORNERPREFERENCE.DWMWCPROUND;
            InteropHelper.DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));


            _viewModel = new MainViewModel();
            _viewModel.PropertyChanged += MainViewModel_PropertyChanged;
            DataContext = _viewModel;
            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            Closing += MainWindow_Closing;
            KeyDown += KeyIsDown;
        }

        private async void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(MainViewModel.CurrentSelectedFilePath):
                    if (_viewModel.CurrentSelectedFilePath != null)
                    {
                        var filePath = _viewModel.CurrentSelectedFilePath.Value;
                        var title = Path.GetFileName(filePath);
                        _viewModel.WindowTitle = title;

                        var titleBarHeight = TitleBar.GetHeight(this);

                        await _viewModel.RenderImageToWindowAsync(filePath, ImageControl, titleBarHeight);
                    }
                    break;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.MainWindowVisibility = Visibility.Collapsed;
            e.Cancel = true;
        }

        private void KeyIsDown(object? sender, KeyEventArgs e)
        {
            if (!e.IsRepeat && _viewModel.CurrentSelectedFilePath != null)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        _viewModel.CurrentSelectedFilePath = _viewModel.CurrentSelectedFilePath.GetPreviousOrLast();
                        e.Handled = true;
                        break;
                    case Key.Right:
                        _viewModel.CurrentSelectedFilePath = _viewModel.CurrentSelectedFilePath.GetNextOrFirst();
                        e.Handled = true;
                        break;
                    default: break;
                }
            }
        }

        private void OnPeekHotkey()
        {
            // if files are selected, open peek with those files (optimization: recognize already opened files)
            // else if window is in focus, close peek

            if (IsActive && _viewModel.MainWindowVisibility == Visibility.Visible)
            {
                _viewModel.ClearSelection();
            }
            else
            {
                _viewModel.TryUpdateSelectedFilePaths();
            }

            BringProcessToForeground();
        }

        private void BringProcessToForeground()
        {
            // Use SendInput hack to allow Activate to work - required to resolve focus issue https://github.com/microsoft/PowerToys/issues/4270
            WindowsInteropHelper.INPUT input = new WindowsInteropHelper.INPUT { Type = WindowsInteropHelper.INPUTTYPE.INPUTMOUSE, Data = { } };
            WindowsInteropHelper.INPUT[] inputs = new WindowsInteropHelper.INPUT[] { input };

            // Send empty mouse event. This makes this thread the last to send input, and hence allows it to pass foreground permission checks
            _ = InteropHelper.SendInput(1, inputs, WindowsInteropHelper.INPUT.Size);

            Activate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.MainWindowVisibility = Visibility.Collapsed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InteropHelper.SetToolWindowStyle(this);
        }

        public void Dispose()
        {
            _viewModel.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
