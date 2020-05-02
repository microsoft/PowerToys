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
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using Mages.Core.Runtime.Converters;
using System.Runtime.InteropServices;

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

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM) : this()
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _viewModel.Save();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs _)
        {
            InitializePosition();

            SearchBox.QueryTextBox.DataContext = _viewModel;
            SearchBox.QueryTextBox.PreviewKeyDown += _launcher_KeyDown;
            SearchBox.QueryTextBox.TextChanged += QueryTextBox_TextChanged;
            SearchBox.QueryTextBox.Focus();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.MainWindowVisibility))
            {
                if (Visibility == System.Windows.Visibility.Visible)
                {
                    Activate();
                    SearchBox.QueryTextBox.Focus();
                    UpdatePosition();
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
            }
            else if (e.PropertyName == nameof(MainViewModel.SystemQueryText))
            {
                this._isTextSetProgramatically = true;
                SearchBox.QueryTextBox.Text = _viewModel.SystemQueryText;
            }
        }

        private void InitializePosition()
        {
            Top = WindowTop();
            Left = WindowLeft();
            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_settings.HideWhenDeactive)
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
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double WindowTop()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - SearchBox.QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
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
        }

        private void UpdateTextBoxToSelectedItem()
        {
            var itemText = _viewModel?.Results?.SelectedItem?.ToString() ?? null;
            if (!String.IsNullOrEmpty(itemText))
            {
                _viewModel.ChangeQueryText(itemText);
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

        private void QueryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (this._isTextSetProgramatically)
            {
                var textBox = ((System.Windows.Controls.TextBox)sender);
                textBox.SelectionStart = textBox.Text.Length;
                this._isTextSetProgramatically = false;
            }
            else
            {
                var text = ((System.Windows.Controls.TextBox)sender).Text;
                if (text == String.Empty)
                {
                    SearchBox.AutoCompleteTextBlock.Text = String.Empty;
                }
                _viewModel.QueryText = text;
                _viewModel.Query();
            }
        }
    }
 }