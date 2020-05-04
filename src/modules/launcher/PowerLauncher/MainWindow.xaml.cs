using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wox.Helper;
using Wox.Infrastructure.UserSettings;
using Wox.ViewModel;

using Screen = System.Windows.Forms.Screen;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            InitializePosition();

            SearchBox.QueryTextBox.DataContext = _viewModel;
            SearchBox.QueryTextBox.PreviewKeyDown += _launcher_KeyDown;
            SearchBox.QueryTextBox.TextChanged += QueryTextBox_TextChanged;
            SearchBox.QueryTextBox.Focus();

            ListBox.DataContext = _viewModel;
            ListBox.SuggestionsList.SelectionChanged += SuggestionsList_SelectionChanged;
            ListBox.SuggestionsList.PreviewMouseLeftButtonUp += SuggestionsList_PreviewMouseLeftButtonUp;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            _viewModel.ColdStartFix();
        }

        private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var result = ((FrameworkElement)e.OriginalSource).DataContext;
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

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
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

                    SearchBox.QueryTextBox.Focus();

                    // to select the text so that the user can continue to type
                    if (!string.IsNullOrEmpty(SearchBox.QueryTextBox.Text))
                    {
                        SearchBox.QueryTextBox.SelectAll();
                    }
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.SystemQueryText))
            {
                _isTextSetProgramatically = true;
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
            if (!string.IsNullOrEmpty(itemText))
            {
                _viewModel.ChangeQueryText(itemText);
            }
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listview = (ListView)sender;
            _viewModel.Results.SelectedItem = (ResultViewModel) listview.SelectedItem;
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                listview.ScrollIntoView(e.AddedItems[0]);
            }

            // To populate the AutoCompleteTextBox as soon as the selection is changed or set.
            // Setting it here instead of when the text is changed as there is a delay in executing the query and populating the result
            SearchBox.AutoCompleteTextBlock.Text = ListView_FirstItem(_viewModel.QueryText);
        }

        private string ListView_FirstItem(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                string selectedItem = _viewModel.Results?.SelectedItem?.ToString();
                int selectedIndex = _viewModel.Results.SelectedIndex;
                if (selectedItem != null && selectedIndex == 0)
                {
                    if (selectedItem.IndexOf(input) == 0)
                    {
                        return selectedItem;
                    }
                }
            }

            return string.Empty;
        }    

        private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isTextSetProgramatically)
            {
                var textBox = ((TextBox)sender);
                textBox.SelectionStart = textBox.Text.Length;
                _isTextSetProgramatically = false;
            }
            else
            {
                var text = ((TextBox)sender).Text;
                if (text == string.Empty)
                {
                    SearchBox.AutoCompleteTextBlock.Text = string.Empty;
                }
                _viewModel.QueryText = text;
                _viewModel.Query();
            }
        }
    }
 }