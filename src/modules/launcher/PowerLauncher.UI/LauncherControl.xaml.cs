using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PowerLauncher.UI
{
    public sealed partial class LauncherControl : UserControl, INotifyPropertyChanged
    {
        private Brush _borderBrush;

        public LauncherControl()
        {
            InitializeComponent();
        }

        public Brush SolidBorderBrush
        {
            get { return _borderBrush; }
            set { Set(ref _borderBrush, value); }
        }

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var NextResultCommand = this.DataContext.GetType().GetMethod("get_SelectNextItemCommand").Invoke(this.DataContext, new object[] { });
            var NextResult = NextResultCommand.GetType().GetMethod("Execute");

            var PreviousResultCommand = this.DataContext.GetType().GetMethod("get_SelectPrevItemCommand").Invoke(this.DataContext, new object[] { });
            var PreviousResult = PreviousResultCommand.GetType().GetMethod("Execute");

            var NextPageResultCommand = this.DataContext.GetType().GetMethod("get_SelectNextPageCommand").Invoke(this.DataContext, new object[] { });
            var NextPageResult = NextResultCommand.GetType().GetMethod("Execute");

            var PreviousPageResultCommand = this.DataContext.GetType().GetMethod("get_SelectPrevPageCommand").Invoke(this.DataContext, new object[] { });
            var PreviousPageResult = PreviousResultCommand.GetType().GetMethod("Execute");

            if (e.Key == Windows.System.VirtualKey.Down)
            {
                NextResult.Invoke(NextResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                PreviousResult.Invoke(PreviousResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.PageDown)
            {
                NextPageResult.Invoke(NextPageResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.PageUp)
            {
                PreviousPageResult.Invoke(PreviousPageResultCommand, new object[] { null });
            }
        }

        private void UserControl_ActualThemeChanged(FrameworkElement sender, object args)
        {
            SolidBorderBrush = Application.Current.Resources["SystemChromeLow"] as SolidColorBrush;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SolidBorderBrush = Application.Current.Resources["SystemChromeLow"] as SolidColorBrush;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}