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
using Wox.Infrastructure.Hotkey;
using Wox.ViewModel;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
            vm.MainWindowVisibilityChanged += (o, e) =>
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
            };
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon { Text = Infrastructure.Wox.Name, Icon = Properties.Resources.app, Visible = true };
            _notifyIcon.Click += (o, e) => App.API.ShowApp();
            var open = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayOpen"));
            open.Click += (o, e) => App.API.ShowApp();
            var setting = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTraySettings"));
            setting.Click += (o, e) => App.API.OpenSettingDialog();
            var about = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayAbout"));
            about.Click += (o, e) => App.API.OpenSettingDialog("about");
            var exit = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayExit"));
            exit.Click += (o, e) => Close();
            MenuItem[] childen = { open, setting, about, exit };
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

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (null == vm) return;
            //when alt is pressed, the real key should be e.SystemKey
            var key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key)
            {
                case Key.Escape:
                    vm.EscCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Tab:
                    if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
                    {
                        vm.SelectPrevItemCommand.Execute(null);
                    }
                    else
                    {
                        vm.SelectNextItemCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;

                case Key.N:
                case Key.J:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.SelectNextItemCommand.Execute(null);
                    }
                    break;

                case Key.P:
                case Key.K:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.SelectPrevItemCommand.Execute(null);
                    }
                    break;

                case Key.O:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.LoadContextMenuCommand.Execute(null);
                    }
                    break;

                case Key.Enter:
                    if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
                    {
                        vm.LoadContextMenuCommand.Execute(null);
                    }
                    else
                    {
                        vm.OpenResultCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.DisplayNextQueryCommand.Execute(null);
                    }
                    else
                    {
                        vm.SelectNextItemCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.DisplayPrevQueryCommand.Execute(null);
                    }
                    else
                    {
                        vm.SelectPrevItemCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;

                case Key.D:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.SelectNextPageCommand.Execute(null);
                    }
                    break;

                case Key.PageDown:
                    vm.SelectNextPageCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.U:
                    if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
                    {
                        vm.SelectPrevPageCommand.Execute(null);
                    }
                    break;

                case Key.PageUp:
                    vm.SelectPrevPageCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.F1:
                    vm.StartHelpCommand.Execute(null);
                    break;

                case Key.D1:

                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(0);
                    }
                    break;

                case Key.D2:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(1);
                    }
                    break;

                case Key.D3:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(2);
                    }
                    break;

                case Key.D4:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(3);
                    }
                    break;

                case Key.D5:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(4);
                    }
                    break;
                case Key.D6:
                    if (GlobalHotkey.Instance.CheckModifiers().AltPressed)
                    {
                        vm.OpenResultCommand.Execute(5);
                    }
                    break;
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