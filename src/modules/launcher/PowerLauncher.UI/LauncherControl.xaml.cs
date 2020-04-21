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

        private void UserControl_ActualThemeChanged(FrameworkElement sender, object args)
        {
            SolidBorderBrush = Application.Current.Resources["SystemControlHighlightAccentBrush"] as SolidColorBrush;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SolidBorderBrush = Application.Current.Resources["SystemControlHighlightAccentBrush"] as SolidColorBrush;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}