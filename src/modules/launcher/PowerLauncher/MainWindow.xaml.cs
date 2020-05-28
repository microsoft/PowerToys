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
using Windows.System;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using Mages.Core.Runtime.Converters;
using System.Runtime.InteropServices;
using Microsoft.PowerLauncher.Telemetry;
using System.Timers;
using Microsoft.PowerToys.Telemetry;
using System.Windows.Controls;

namespace PowerLauncher
{
    public partial class MainWindow
    {

        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private Settings _settings;
        private MainViewModel _viewModel;
        private bool _isTextSetProgramatically;
        const int ROW_HEIGHT = 75;
        const int MAX_LIST_HEIGHT = 300;
        bool isDPIChanged = false;
        bool _deletePressed = false;
        Timer _firstDeleteTimer = new Timer();


        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
            InitializeComponent();

            _firstDeleteTimer.Elapsed += CheckForFirstDelete;
            _firstDeleteTimer.Interval = 1000;

        }

        private void CheckForFirstDelete(object sender, ElapsedEventArgs e)
        {
            _firstDeleteTimer.Stop();
            if (_deletePressed)
            {
                PowerToysTelemetry.Log.WriteEvent(new LauncherFirstDeleteEvent());
            }

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
            WindowsInteropHelper.DisableControlBox(this);

            InitializePosition();

            SearchBox.QueryTextBox.DataContext = _viewModel;
            SearchBox.QueryTextBox.PreviewKeyDown += _launcher_KeyDown;
            SearchBox.QueryTextBox.TextChanged += QueryTextBox_TextChanged;
            SearchBox.QueryTextBox.Focus();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void QueryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (this._isTextSetProgramatically)
            {
                var textBox = ((TextBox)sender);
                textBox.SelectionStart = textBox.Text.Length;

                this._isTextSetProgramatically = false;
            }
            else
            {
                var text = ((TextBox)sender).Text;
                if (text == string.Empty)
                {
                    SearchBox.AutoCompleteTextBlock.Text = String.Empty;
                }

                _viewModel.QueryText = text;
                var latestTimeOfTyping = DateTime.Now;

                Task.Run(() => DelayedCheck(latestTimeOfTyping, text));
                s_lastTimeOfTyping = latestTimeOfTyping;
            }
        }

        private void InitializePosition()
        {
            Top = WindowTop();
            Left = WindowLeft();
            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.MainWindowVisibility))
            {
                if (Visibility == Visibility.Visible)
                {
                    _deletePressed = false;
                    _firstDeleteTimer.Start();
                    Activate();
                    UpdatePosition();
                    SearchBox.QueryTextBox.Focus();
                    _settings.ActivateTimes++;
                    if (!_viewModel.LastQuerySelected)
                    {
                        _viewModel.LastQuerySelected = true;
                    }

                    // to select the text so that the user can continue to type
                    if (!String.IsNullOrEmpty(SearchBox.QueryTextBox.Text))
                    {
                        SearchBox.QueryTextBox.SelectAll();
                    }
                }
                else
                {
                    _firstDeleteTimer.Stop();
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.SystemQueryText))
            {
                this._isTextSetProgramatically = true;
                SearchBox.QueryTextBox.Text = _viewModel.SystemQueryText;
            }
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
            if (_settings.HideWhenDeactivated)
            {
                if (isDPIChanged)
                {
                    isDPIChanged = false;
                    InitializePosition();
                }
                else
                {
                    Hide();
                }
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
                double prevTop = Top;
                double prevLeft = Left;               
                Top = WindowTop();
                Left = WindowLeft();
                if (prevTop != Top || prevLeft != Left)
                {
                    isDPIChanged = true;
                }
                else
                {
                    isDPIChanged = false;
                }
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

        /// <summary>
        /// Calculates X co-ordinate of main window top left corner.
        /// </summary>
        /// <returns>X co-ordinate of main window top left corner</returns>
        private double WindowLeft()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dpi1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dpi2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dpi2.X - this.Width) / 2 + dpi1.X;
            return left;
        }

        /// <summary>
        /// Calculates Y co-ordinate of main window top left corner 
        /// </summary>
        /// <returns>Y co-ordinate of main window top left corner</returns>
        private double WindowTop()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dpi1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dpi2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dpi2.Y - this.SearchBox.Height) / 4 + dpi1.Y;
            return top;
        }

        private void UserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SolidBorderBrush")
            {
                if (_resultList != null)
                {
                    Windows.UI.Xaml.Media.SolidColorBrush borderBrush = _resultList.SolidBorderBrush as Windows.UI.Xaml.Media.SolidColorBrush;
                    Color borderColor = Color.FromArgb(borderBrush.Color.A, borderBrush.Color.R, borderBrush.Color.G, borderBrush.Color.B);
                    SolidColorBrush solidBorderBrush = new SolidColorBrush(borderColor);
                                      
                    this.SearchBoxBorder.BorderBrush = solidBorderBrush;
                    this.SearchBoxBorder.Background = solidBorderBrush;
                    this.ListBoxBorder.BorderBrush = solidBorderBrush;
                    this.ListBoxBorder.Background = solidBorderBrush; 

                }
            }
            else if(e.PropertyName == "PrimaryTextColor")
            {
                if (_resultList != null)
                {
                    Windows.UI.Xaml.Media.SolidColorBrush primaryTextBrush = _resultList.PrimaryTextColor as Windows.UI.Xaml.Media.SolidColorBrush;
                    Color primaryTextColor = Color.FromArgb(primaryTextBrush.Color.A, primaryTextBrush.Color.R, primaryTextBrush.Color.G, primaryTextBrush.Color.B);
                    SolidColorBrush solidPrimaryTextBrush = new SolidColorBrush(primaryTextColor);

                    this.SearchBox.QueryTextBox.Foreground = solidPrimaryTextBrush;
                    this.SearchBox.QueryTextBox.CaretBrush = solidPrimaryTextBrush;
                    this.SearchBox.AutoCompleteTextBlock.Foreground = solidPrimaryTextBrush;
                    this.SearchBox.SearchLogo.Foreground = solidPrimaryTextBrush;
                }
            }
        }

        private UI.ResultList _resultList = null;
        private void WindowsXamlHostListView_ChildChanged(object sender, EventArgs ev)
        {
            if (sender == null) return; 

            var host = (WindowsXamlHost)sender;
            _resultList = (UI.ResultList)host.Child;
            _resultList.DataContext = _viewModel;
            _resultList.Tapped += SuggestionsList_Tapped;
            _resultList.SuggestionsList.Loaded += SuggestionsList_Loaded;
            _resultList.SuggestionsList.SelectionChanged += SuggestionsList_SelectionChanged;
            _resultList.SuggestionsList.ContainerContentChanging += SuggestionList_UpdateListSize;
            _resultList.PropertyChanged += UserControl_PropertyChanged;
        }

        private void SuggestionsList_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _viewModel.ColdStartFix();
        }

        private void _launcher_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && Keyboard.IsKeyDown(Key.LeftShift))
            {
                _viewModel.SelectPrevTabItemCommand.Execute(null);
                UpdateTextBoxToSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                _viewModel.SelectNextTabItemCommand.Execute(null);
                UpdateTextBoxToSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _viewModel.SelectNextItemCommand.Execute(null);
                UpdateTextBoxToSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _viewModel.SelectPrevItemCommand.Execute(null);
                UpdateTextBoxToSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                _viewModel.SelectNextPageCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                _viewModel.SelectPrevPageCommand.Execute(null);
                e.Handled = true;
            }
            else if( e.Key == Key.Back)
            {
                _deletePressed = true;
            }
        }

        private void UpdateTextBoxToSelectedItem()
        {
            var itemText = _viewModel?.Results?.SelectedItem?.ToString() ?? null;
            if (!String.IsNullOrEmpty(itemText))
            {
                _viewModel.ChangeQueryText(itemText);
            }
        }

        private void SuggestionsList_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
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
        private void SuggestionList_UpdateListSize(object sender, Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs e)
        {
            int count = _viewModel?.Results?.Results.Count ?? 0;
            int displayCount = Math.Min(count, _settings.MaxResultsToShow);
            _resultList.Height = displayCount * ROW_HEIGHT;
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
            SearchBox.AutoCompleteTextBlock.Text = ListView_FirstItem(_viewModel.QueryText);
        }

        private const int millisecondsToWait = 100;
        private static DateTime s_lastTimeOfTyping;

        private string ListView_FirstItem(String input)
        {
            if (!String.IsNullOrEmpty(input))
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

        private void QueryTextBox_TextChangedProgramatically(object sender, Windows.UI.Xaml.Controls.TextChangedEventArgs e)
        {
            
        }

        private async Task DelayedCheck(DateTime latestTimeOfTyping, string text)
        {
            await Task.Delay(millisecondsToWait);
            if (latestTimeOfTyping.Equals(s_lastTimeOfTyping))
            {
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _viewModel.Query();
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