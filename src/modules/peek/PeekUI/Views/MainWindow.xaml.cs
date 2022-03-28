using interop;
using ModernWpf.Controls;
using PeekUI.Extensions;
using PeekUI.Helpers;
using PeekUI.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace PeekUI.Views
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

            this.RoundCorners();

            _viewModel = new MainViewModel(ImageControl);
            _viewModel.PropertyChanged += MainViewModel_PropertyChanged;

            DataContext = _viewModel;

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            Closing += MainWindow_Closing;
            KeyDown += KeyIsDown;
        }

        public void Dispose()
        {
            _viewModel.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.SetToolStyle();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.MainWindowData.Visibility = Visibility.Collapsed;
            _viewModel.MainWindowData.TitleBarHeight = TitleBar.GetHeight(this);
            _viewModel.ImageControl = ImageControl;
        }

        private async void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.CurrentSelectedFilePath):
                    if (_viewModel.CurrentSelectedFilePath != null)
                    {
                        await _viewModel.RenderImageToWindowAsync(_viewModel.CurrentSelectedFilePath.Value);
                    }
                    break;
            }
        }

        private void OnPeekHotkey()
        {
            if (IsActive && _viewModel.MainWindowData.Visibility == Visibility.Visible)
            {
                _viewModel.ClearSelection();
            }
            else
            {
                _viewModel.TryUpdateSelectedFilePaths();
            }

            this.BringToForeground();
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

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.MainWindowData.Visibility = Visibility.Collapsed;
            e.Cancel = true;
        }
    }
}
