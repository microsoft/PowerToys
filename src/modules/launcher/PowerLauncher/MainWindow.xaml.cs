using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure.UserSettings;
using Wox.ViewModel;

using Screen = System.Windows.Forms.Screen;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Core;
using System.Windows.Media;

namespace PowerLauncher
{
    public partial class MainWindow
    {

        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private Settings _settings;
        private MainViewModel _viewModel;

        const int ROW_COUNT = 4;
        const int ROW_HEIGHT = 75;
        const int MAX_LIST_HEIGHT = 300;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
            InitializeComponent();

        }
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _viewModel.Save();
        }

        private void OnInitialized(object sender, EventArgs e)
        {
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs _)
        {
            InitializePosition();
        }

        private void InitializePosition()
        {
            //Top = WindowTop();
            Left = WindowLeft();
            //_settings.WindowTop = Top;
            _settings.WindowLeft = Left;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
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

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_settings.HideWhenDeactive)
            {
                Hide();
            }
        }

        private void UpdatePosition()
        {
            if (_settings.RememberLastLaunchLocation)
            {
                Left = _settings.WindowLeft;
                Top = _settings.WindowTop;
            }
            else
            {
                Left = WindowLeft();
                //Top = WindowTop();
            }
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_settings.RememberLastLaunchLocation)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
        }

        private double WindowLeft()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }


        private PowerLauncher.UI.LauncherControl _launcher = null;
        private void WindowsXamlHostTextBox_ChildChanged(object sender, EventArgs ev)
        {
            if (sender == null) return;

            var host = (WindowsXamlHost)sender;
            _launcher = (PowerLauncher.UI.LauncherControl)host.Child;
            _launcher.DataContext = _viewModel;
            _launcher.KeyDown += _launcher_KeyDown;
            _launcher.TextBox.TextChanged += QueryTextBox_TextChanged;
            _launcher.TextBox.Loaded += TextBox_Loaded;
            _launcher.PropertyChanged += UserControl_PropertyChanged;
            _viewModel.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.MainWindowVisibility))
                {
                    if (Visibility == System.Windows.Visibility.Visible)
                    {
                        Activate();
                        UpdatePosition();
                        _settings.ActivateTimes++;
                        if (!_viewModel.LastQuerySelected)
                        {
                            _viewModel.LastQuerySelected = true;
                        }          
                    }
                }
            };           
        }

        private void UserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SolidBorderBrush")
            {
                if (_launcher != null)
                {
                    Windows.UI.Xaml.Media.SolidColorBrush uwpBrush = _launcher.SolidBorderBrush as Windows.UI.Xaml.Media.SolidColorBrush;
                    System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(uwpBrush.Color.A, uwpBrush.Color.R, uwpBrush.Color.G, uwpBrush.Color.B);
                    this.SearchBoxBorder.BorderBrush = new SolidColorBrush(borderColor);
                    this.ListBoxBorder.BorderBrush = new SolidColorBrush(borderColor);
                }
            }
        }

        private void TextBox_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            tb.Focus(FocusState.Programmatic);
            _viewModel.MainWindowVisibility = System.Windows.Visibility.Collapsed;
        }

        private UI.ResultList _resultList = null;
        private void WindowsXamlHostListView_ChildChanged(object sender, EventArgs ev)
        {
            if (sender == null) return; 

            var host = (WindowsXamlHost)sender;
            _resultList = (UI.ResultList)host.Child;
            _resultList.DataContext = _viewModel;
            _resultList.Tapped += SuggestionsList_Tapped;
            _resultList.SuggestionsList.SelectionChanged += SuggestionsList_SelectionChanged;
            _resultList.SuggestionsList.ContainerContentChanging += SuggestionList_UpdateListSize;
        }

        private bool IsKeyDown(VirtualKey key)
        {
            var keyState = CoreWindow.GetForCurrentThread().GetKeyState(key);
            return (keyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private void _launcher_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab && IsKeyDown(VirtualKey.Shift))
            {
                _viewModel.SelectPrevTabItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Tab)
            {
                    _viewModel.SelectNextTabItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                _viewModel.SelectNextItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Up)
            {
                _viewModel.SelectPrevItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                _viewModel.SelectNextPageCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                _viewModel.SelectPrevPageCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SuggestionsList_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var result = ((Windows.UI.Xaml.FrameworkElement)e.OriginalSource).DataContext;
            if (result != null)
            {
                var resultVM = result as ResultViewModel;

                //This may be null if the tapped item was one of the context buttons (run as admin etc).
                if (resultVM != null)
                {
                    _viewModel.Results.SelectedItem = resultVM;
                    _viewModel.OpenResultCommand.Execute(null);
                }
            }
        }

        /* Note: This function has been added because a white-background was observed when the list resized,
         * when the number of elements were lesser than the maximum capacity of the list (ie. 4).
         * Binding Height/MaxHeight Properties did not solve this issue.
         */
        private void SuggestionList_UpdateListSize(object sender, ContainerContentChangingEventArgs e)
        {
            int count = _viewModel?.Results?.Results.Count ?? 0;
            int maxHeight = count < ROW_COUNT ? count * ROW_HEIGHT : MAX_LIST_HEIGHT;
            _resultList.Height = maxHeight;
        }

        private void SuggestionsList_SelectionChanged(object sender, Windows.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            Windows.UI.Xaml.Controls.ListView listview = (Windows.UI.Xaml.Controls.ListView)sender;
            _viewModel.Results.SelectedItem = (ResultViewModel) listview.SelectedItem;
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                listview.ScrollIntoView(e.AddedItems[0]);
            }

            // To populate the AutoCompleteTextBox as soon as the selection is changed or set.
            // Setting it here instead of when the text is changed as there is a delay in executing the query and populating the result
            _launcher.AutoCompleteTextBox.PlaceholderText = ListView_FirstItem(_viewModel.QueryText);

        }

        private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ResultViewModel result = e?.ClickedItem as ResultViewModel;
            if(result != null)
            {
                _viewModel.Results.SelectedItem = result;
                _viewModel.OpenResultCommand.Execute(null);
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args != null && args.ChosenSuggestion != null)
            {
                ResultViewModel result = (ResultViewModel)args.ChosenSuggestion;
                if (result != null)
                {
                    _viewModel.Results.SelectedItem = result;
                    _viewModel.OpenResultCommand.Execute(null);
                }
            }
        }

        private const int millisecondsToWait = 200;
        private static DateTime s_lastTimeOfTyping;

        private string ListView_FirstItem(String input)
        {
            string s = input;
            if (s.Length > 0)
            {
                String selectedItem = _viewModel.Results?.SelectedItem?.ToString();
                int selectedIndex = _viewModel.Results.SelectedIndex;
                if (selectedItem != null && selectedIndex == 0)
                {
                    if (selectedItem.IndexOf(input) == 0)
                    {
                        return selectedItem;
                    }
                }
            }

            return String.Empty;
        }


        private void QueryTextBox_TextChanged(object sender, Windows.UI.Xaml.Controls.TextChangedEventArgs e)
        {
            var latestTimeOfTyping = DateTime.Now;
            var text = ((Windows.UI.Xaml.Controls.TextBox)sender).Text;
            Task.Run(() => DelayedCheck(latestTimeOfTyping, text));
            s_lastTimeOfTyping = latestTimeOfTyping;

            //To clear the auto-suggest immediately instead of waiting for selection changed
            if(text == String.Empty)
            {
                _launcher.AutoCompleteTextBox.PlaceholderText = String.Empty;
            }
        }

        private async Task DelayedCheck(DateTime latestTimeOfTyping, string text)
        {
            await Task.Delay(millisecondsToWait);
            if (latestTimeOfTyping.Equals(s_lastTimeOfTyping))
            {
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                 {
                     _viewModel.QueryText = text;
                 }));               
            }
        }

        private void WindowsXamlHost_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //    if (sender != null && e.OriginalSource != null)
            //    {
            //        //var r = (ResultListBox)sender;
            //        //var d = (DependencyObject)e.OriginalSource;
            //        //var item = ItemsControl.ContainerFromElement(r, d) as ListBoxItem;
            //        //var result = (ResultViewModel)item?.DataContext;
            //        //if (result != null)
            //        //{
            //        //    if (e.ChangedButton == MouseButton.Left)
            //        //    {
            //        //        _viewModel.OpenResultCommand.Execute(null);
            //        //    }
            //        //    else if (e.ChangedButton == MouseButton.Right)
            //        //    {
            //        //        _viewModel.LoadContextMenuCommand.Execute(null);
            //        //    }
            //        //}
            //    }
        }
    }
 }