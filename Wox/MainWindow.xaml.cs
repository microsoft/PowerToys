using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.ViewModel;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class MainWindow
    {

        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private Settings _settings;
        private NotifyIcon _notifyIcon;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            InitializeComponent();
            _settings = settings;
        }
        public MainWindow()
        {
            InitializeComponent();
        }
        private void OnClosing(object sender, CancelEventArgs e)
        {
            _notifyIcon.Visible = false;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            var vm = (MainViewModel)DataContext;
            vm.Save();
        }



        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            InitProgressbarAnimation();
            WindowIntelopHelper.DisableControlBox(this);
            ThemeManager.Instance.ChangeTheme(_settings.Theme);
            InitializeNotifyIcon();

            var vm = (MainViewModel)DataContext;
            RegisterEvents(vm);

            // happlebao todo delete
            vm.Left = GetWindowsLeft();
            vm.Top = GetWindowsTop();
            vm.MainWindowVisibility = _settings.HideOnStartup ? Visibility.Hidden : Visibility.Visible;
        }

        private void RegisterEvents(MainViewModel vm)
        {
            vm.TextBoxSelected += (o, e) => QueryTextBox.SelectAll();
            vm.CursorMovedToEnd += (o, e) =>
            {
                QueryTextBox.Focus();
                QueryTextBox.CaretIndex = QueryTextBox.Text.Length;
            };
            vm.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(vm.MainWindowVisibility))
                {
                    if (vm.MainWindowVisibility.IsVisible())
                    {
                        Activate();
                        QueryTextBox.Focus();
                        Left = GetWindowsLeft();
                        Top = GetWindowsTop();
                        _settings.ActivateTimes++;
                    }
                    else
                    {
                        _settings.WindowLeft = Left;
                        _settings.WindowTop = Top;
                    }
                }
            };
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon { Text = Infrastructure.Constant.Wox, Icon = Properties.Resources.app, Visible = true };
            _notifyIcon.Click += (o, e) => App.API.ShowApp();
            var open = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayOpen"));
            open.Click += (o, e) => App.API.ShowApp();
            var setting = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTraySettings"));
            setting.Click += (o, e) => App.API.OpenSettingDialog();
            var exit = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayExit"));
            exit.Click += (o, e) => Close();
            MenuItem[] childen = { open, setting, exit };
            _notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private double GetWindowsLeft()
        {
            if (_settings.RememberLastLaunchLocation)
            {
                return _settings.WindowLeft;
            }

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowIntelopHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowIntelopHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double GetWindowsTop()
        {
            if (_settings.RememberLastLaunchLocation)
            {
                return _settings.WindowTop;
            }

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowIntelopHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowIntelopHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - ActualHeight) / 4 + dip1.Y;
            return top;
        }

        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            _progressBarStoryboard.Children.Add(da);
            _progressBarStoryboard.Children.Add(da1);
            _progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            ProgressBar.Visibility = Visibility.Hidden;
            ProgressBar.BeginStoryboard(_progressBarStoryboard);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_settings.HideWhenDeactive)
            {
                App.API.HideApp();
            }
        }

        private void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender != null && e.OriginalSource != null)
            {
                var r = (ResultListBox)sender;
                var d = (DependencyObject)e.OriginalSource;
                var item = ItemsControl.ContainerFromElement(r, d) as ListBoxItem;
                var result = (ResultViewModel)item?.DataContext;
                if (result != null)
                {
                    var vm = DataContext as MainViewModel;
                    if (vm != null)
                    {
                        if (e.ChangedButton == MouseButton.Left)
                        {
                            vm.OpenResultCommand.Execute(null);
                        }
                        else if (e.ChangedButton == MouseButton.Right)
                        {
                            vm.LoadContextMenuCommand.Execute(null);
                        }
                    }
                }
            }
        }


        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files[0].ToLower().EndsWith(".wox"))
                {
                    PluginManager.InstallPlugin(files[0]);
                }
                else
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidWoxPluginFileFormat"));
                }
            }
            e.Handled = false;
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            App.API.OpenSettingDialog();
        }
    }
}