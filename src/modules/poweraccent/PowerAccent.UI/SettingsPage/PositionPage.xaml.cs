using PowerAccent.Core.Services;
using System.Windows;
using System.Windows.Controls;

namespace PowerAccent.UI.SettingsPage
{
    /// <summary>
    /// Logique d'interaction pour Position.xaml
    /// </summary>
    public partial class PositionPage : Page
    {
        private readonly SettingsService _settingService = new SettingsService();

        public PositionPage()
        {
            InitializeComponent();
            RefreshPosition();
        }

        private void Position_Up_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.Top;
            RefreshPosition();
        }
        private void Position_Down_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.Bottom;
            RefreshPosition();
        }
        private void Position_Left_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.Left;
            RefreshPosition();
        }
        private void Position_Right_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.Right;
            RefreshPosition();
        }
        private void Position_UpLeft_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.TopLeft;
            RefreshPosition();
        }
        private void Position_UpRight_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.TopRight;
            RefreshPosition();
        }
        private void Position_DownLeft_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.BottomLeft;
            RefreshPosition();
        }
        private void Position_DownRight_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.BottomRight;
            RefreshPosition();
        }
        private void Position_Center_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.Position = Position.Center;
            RefreshPosition();
        }

        private void RefreshPosition()
        {
            var position = _settingService.Position;
            Position_Up.IsChecked = position == Position.Top;
            Position_Down.IsChecked = position == Position.Bottom;
            Position_Left.IsChecked = position == Position.Left;
            Position_Right.IsChecked = position == Position.Right;
            Position_UpRight.IsChecked = position == Position.TopRight;
            Position_UpLeft.IsChecked = position == Position.TopLeft;
            Position_DownRight.IsChecked = position == Position.BottomRight;
            Position_DownLeft.IsChecked = position == Position.BottomLeft;
            Position_Center.IsChecked = position == Position.Center;

            (Application.Current.MainWindow as Selector).RefreshSettings();
        }
    }
}
