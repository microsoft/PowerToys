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
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Wox.ViewModel;
using Wox.Plugin;

namespace Wox
{
    public partial class MainWindow
    {

        #region Properties

        private readonly Storyboard progressBarStoryboard = new Storyboard();
        private NotifyIcon notifyIcon;
        
        

        #endregion
        

        public MainWindow()
        {
            InitializeComponent();
            
            InitialTray();

            //pnlResult.ItemDropEvent += pnlResult_ItemDropEvent;
            Closing += MainWindow_Closing;
        }

        //void pnlResult_ItemDropEvent(Result result, IDataObject dropDataObject, DragEventArgs args)
        //{
        //    PluginPair pluginPair = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
        //    if (ResultItemDropEvent != null && pluginPair != null)
        //    {
        //        foreach (var delegateHandler in ResultItemDropEvent.GetInvocationList())
        //        {
        //            if (delegateHandler.Target == pluginPair.Plugin)
        //            {
        //                delegateHandler.DynamicInvoke(result, dropDataObject, args);
        //            }
        //        }
        //    }
        //}

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            UserSettingStorage.Instance.WindowLeft = Left;
            UserSettingStorage.Instance.WindowTop = Top;
            UserSettingStorage.Instance.Save();
            e.Cancel = true;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Theme.ChangeTheme(UserSettingStorage.Instance.Theme);
            InternationalizationManager.Instance.ChangeLanguage(UserSettingStorage.Instance.Language);

            Left = GetWindowsLeft();
            Top = GetWindowsTop();

            InitProgressbarAnimation();
            WindowIntelopHelper.DisableControlBox(this);
            CheckUpdate();
        }

        private double GetWindowsLeft()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowLeft;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            UserSettingStorage.Instance.WindowLeft = (dipPoint.X - ActualWidth)/2;
            return UserSettingStorage.Instance.WindowLeft;
        }

        private double GetWindowsTop()
        {
            if (UserSettingStorage.Instance.RememberLastLaunchLocation) return UserSettingStorage.Instance.WindowTop;

            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipPoint = WindowIntelopHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            UserSettingStorage.Instance.WindowTop = (dipPoint.Y - tbQuery.ActualHeight)/4;
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
            progressBarStoryboard.Children.Add(da);
            progressBarStoryboard.Children.Add(da1);
            progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            progressBar.Visibility = Visibility.Hidden;
            progressBar.BeginStoryboard(progressBarStoryboard);
        }

        private void InitialTray()
        {
            //notifyIcon = new NotifyIcon { Text = "Wox", Icon = Properties.Resources.app, Visible = true };
            //notifyIcon.Click += (o, e) => ShowWox();
            //var open = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayOpen"));
            //open.Click += (o, e) => ShowWox();
            //var setting = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTraySettings"));
            //setting.Click += (o, e) => OpenSettingDialog();
            //var about = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayAbout"));
            //about.Click += (o, e) => OpenSettingDialog("about");
            //var exit = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayExit"));
            //exit.Click += (o, e) => CloseApp();
            //MenuItem[] childen = { open, setting, about, exit };
            //notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private void Border_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (UserSettingStorage.Instance.HideWhenDeactive)
            {
                //TODO:Hide the window when deactivated
                //HideWox();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //The code here is to supress the conflict of Window.InputBinding and ListBox native Down/Up Key handle
            var vm = this.DataContext as MainViewModel;
            //when alt is pressed, the real key should be e.SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key)
            {
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
                case Key.PageDown:
                    vm.SelectNextPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    vm.SelectPrevPageCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        //TODO: Colin - Figure out how to raise BackKeyDownEvent?
        //        case Key.Back:
        //            if (BackKeyDownEvent != null)
        //            {
        //                BackKeyDownEvent(new WoxKeyDownEventArgs
        //                {
        //                    Query = tbQuery.Text,
        //                    keyEventArgs = e
        //                });
        //            }
        //            break;

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
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

        private void TbQuery_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}