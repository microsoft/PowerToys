using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Hotkey;
using Wox.ViewModel;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class MainWindow
    {

        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();

        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            UserSettingStorage.Instance.WindowLeft = Left;
            UserSettingStorage.Instance.WindowTop = Top;
            UserSettingStorage.Instance.Save();
            e.Cancel = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            CheckUpdate();

            ThemeManager.Theme.ChangeTheme(UserSettingStorage.Instance.Theme);
            InternationalizationManager.Instance.ChangeLanguage(UserSettingStorage.Instance.Language);

            InitProgressbarAnimation();
            WindowIntelopHelper.DisableControlBox(this);

            var vm = (MainViewModel)DataContext;
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
                }
            };

            vm.Left = GetWindowsLeft();
            vm.Top = GetWindowsTop();
            vm.MainWindowVisibility = Visibility.Visible;
        }

        private double GetWindowsLeft()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowLeft;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            UserSettingStorage.Instance.WindowLeft = (dipPoint.X - ActualWidth) / 2;
            return UserSettingStorage.Instance.WindowLeft;
        }

        private double GetWindowsTop()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowTop;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            UserSettingStorage.Instance.WindowTop = (dipPoint.Y - QueryTextBox.ActualHeight) / 4;
            return UserSettingStorage.Instance.WindowTop;
        }

        private void CheckUpdate()
        {
            UpdaterManager.Instance.PrepareUpdateReady += OnPrepareUpdateReady;
            UpdaterManager.Instance.UpdateError += OnUpdateError;
            UpdaterManager.Instance.CheckUpdate();
        }

        void OnUpdateError(object sender, EventArgs e)
        {
            string updateError = InternationalizationManager.Instance.GetTranslation("update_wox_update_error");
            MessageBox.Show(updateError);
        }

        private void OnPrepareUpdateReady(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                new WoxUpdate().ShowDialog();
            });
        }

        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(progressBar.X2, ActualWidth + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(progressBar.X1, ActualWidth, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            _progressBarStoryboard.Children.Add(da);
            _progressBarStoryboard.Children.Add(da1);
            _progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            progressBar.Visibility = Visibility.Hidden;
            progressBar.BeginStoryboard(_progressBarStoryboard);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (UserSettingStorage.Instance.HideWhenDeactive)
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
                        vm.CtrlOCommand.Execute(null);
                    }
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

                case Key.Back:
                    vm.BackCommand.Execute(e);
                    break;

                case Key.F1:
                    vm.StartHelpCommand.Execute(null);
                    break;

                case Key.Enter:
                    if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
                    {
                        vm.ShiftEnterCommand.Execute(null);
                    }
                    else
                    {
                        vm.OpenResultCommand.Execute(null);
                    }
                    e.Handled = true;
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
    }
}