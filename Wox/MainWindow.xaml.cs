using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using Stopwatch = Wox.Infrastructure.Stopwatch;
using ToolTip = System.Windows.Controls.ToolTip;
using Wox.ViewModel;

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

        private void ShowWox(bool selectAll = true)
        {
            //UserSettingStorage.Instance.IncreaseActivateTimes();
            //Left = GetWindowsLeft();
            //Top = GetWindowsTop();

            //Show();
            //Activate();
            //Focus();
            //tbQuery.Focus();
            //ResetQueryHistoryIndex();
            //if (selectAll) tbQuery.SelectAll();
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (UserSettingStorage.Instance.HideWhenDeactive)
            {
                //TODO:Hide the window when deactivated
                //HideWox();
            }
        }

        //private void TbQuery_OnPreviewKeyDown(object sender, KeyEventArgs e)
        //{
        //    //when alt is pressed, the real key should be e.SystemKey
        //    Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
        //    switch (key)
        //    {
        //        case Key.Escape:
        //            if (IsInContextMenuMode)
        //            {
        //                BackToResultMode();
        //            }
        //            else
        //            {
        //                HideWox();
        //            }
        //            e.Handled = true;
        //            break;

        //        case Key.Tab:
        //            if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
        //            {
        //                SelectPrevItem();
        //            }
        //            else
        //            {
        //                SelectNextItem();
        //            }
        //            e.Handled = true;
        //            break;

        //        case Key.N:
        //        case Key.J:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                SelectNextItem();
        //            }
        //            break;

        //        case Key.P:
        //        case Key.K:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                SelectPrevItem();
        //            }
        //            break;

        //        case Key.O:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                if (IsInContextMenuMode)
        //                {
        //                    BackToResultMode();
        //                }
        //                else
        //                {
        //                    ShowContextMenu(GetActiveResult());
        //                }
        //            }
        //            break;

        //        case Key.Down:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                DisplayNextQuery();
        //            }
        //            else
        //            {
        //                SelectNextItem();
        //            }
        //            e.Handled = true;
        //            break;

        //        case Key.Up:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                DisplayPrevQuery();
        //            }
        //            else
        //            {
        //                SelectPrevItem();
        //            }
        //            e.Handled = true;
        //            break;

        //        case Key.D:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                pnlResult.SelectNextPage();
        //            }
        //            break;

        //        case Key.PageDown:
        //            pnlResult.SelectNextPage();
        //            e.Handled = true;
        //            break;

        //        case Key.U:
        //            if (GlobalHotkey.Instance.CheckModifiers().CtrlPressed)
        //            {
        //                pnlResult.SelectPrevPage();
        //            }
        //            break;

        //        case Key.PageUp:
        //            pnlResult.SelectPrevPage();
        //            e.Handled = true;
        //            break;

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

        //        case Key.F1:
        //            Process.Start("http://doc.getwox.com");
        //            break;

        //        case Key.Enter:
        //            Result activeResult = GetActiveResult();
        //            if (GlobalHotkey.Instance.CheckModifiers().ShiftPressed)
        //            {
        //                ShowContextMenu(activeResult);
        //            }
        //            else
        //            {
        //                SelectResult(activeResult);
        //            }
        //            e.Handled = true;
        //            break;

        //        case Key.D1:
        //            SelectItem(1);
        //            break;

        //        case Key.D2:
        //            SelectItem(2);
        //            break;

        //        case Key.D3:
        //            SelectItem(3);
        //            break;

        //        case Key.D4:
        //            SelectItem(4);
        //            break;

        //        case Key.D5:
        //            SelectItem(5);
        //            break;
        //        case Key.D6:
        //            SelectItem(6);
        //            break;

        //    }
        //}

        //private void DisplayPrevQuery()
        //{
        //    var prev = QueryHistoryStorage.Instance.Previous();
        //    DisplayQueryHistory(prev);
        //}

        //private void DisplayNextQuery()
        //{
        //    var nextQuery = QueryHistoryStorage.Instance.Next();
        //    DisplayQueryHistory(nextQuery);
        //}

        //private void DisplayQueryHistory(HistoryItem history)
        //{
        //    if (history != null)
        //    {
        //        var historyMetadata = QueryHistoryStorage.MetaData;
        //        ChangeQueryText(history.Query, true);
        //        var executeQueryHistoryTitle = GetTranslation("executeQuery");
        //        var lastExecuteTime = GetTranslation("lastExecuteTime");
        //        pnlResult.RemoveResultsExcept(historyMetadata);
        //        UpdateResultViewInternal(new List<Result>
        //        {
        //            new Result
        //            {
        //                Title = string.Format(executeQueryHistoryTitle,history.Query),
        //                SubTitle = string.Format(lastExecuteTime,history.ExecutedDateTime),
        //                IcoPath = "Images\\history.png",
        //                PluginDirectory = WoxDirectroy.Executable,
        //                Action = _ =>{
        //                    ChangeQuery(history.Query,true);
        //                    return false;
        //                }
        //            }
        //        }, historyMetadata);
        //    }
        //}

        //private void SelectItem(int index)
        //{
        //    int zeroBasedIndex = index - 1;
        //    SpecialKeyState keyState = GlobalHotkey.Instance.CheckModifiers();
        //    if (keyState.AltPressed || keyState.CtrlPressed)
        //    {
        //        List<Result> visibleResults = pnlResult.GetVisibleResults();
        //        if (zeroBasedIndex < visibleResults.Count)
        //        {
        //            SelectResult(visibleResults[zeroBasedIndex]);
        //        }
        //    }
        //}

        //private bool IsInContextMenuMode
        //{
        //    get { return pnlContextMenu.Visibility == Visibility.Visible; }
        //}

        //private Result GetActiveResult()
        //{
        //    if (IsInContextMenuMode)
        //    {
        //        return pnlContextMenu.GetActiveResult();
        //    }
        //    else
        //    {
        //        return pnlResult.GetActiveResult();
        //    }
        //}

        //private void SelectPrevItem()
        //{
        //    if (IsInContextMenuMode)
        //    {
        //        pnlContextMenu.SelectPrev();
        //    }
        //    else
        //    {
        //        pnlResult.SelectPrev();
        //    }
        //    toolTip.IsOpen = false;
        //}

        //private void SelectNextItem()
        //{
        //    if (IsInContextMenuMode)
        //    {
        //        pnlContextMenu.SelectNext();
        //    }
        //    else
        //    {
        //        pnlResult.SelectNext();
        //    }
        //    toolTip.IsOpen = false;
        //}

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
        }

        private void TbQuery_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}