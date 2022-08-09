// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using interop;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.Telemetry.Events;
using PowerLauncher.ViewModel;
using Wox.Infrastructure.UserSettings;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Log = Wox.Plugin.Logger.Log;
using Screen = System.Windows.Forms.Screen;

namespace PowerLauncher
{
    public partial class MainWindow : IDisposable
    {
        private readonly PowerToysRunSettings _settings;
        private readonly MainViewModel _viewModel;
        private bool _isTextSetProgrammatically;
        private bool _deletePressed;
        private HwndSource _hwndSource;
        private Timer _firstDeleteTimer = new Timer();
        private bool _coldStateHotkeyPressed;
        private bool _disposedValue;
        private IDisposable _reactiveSubscription;

        public MainWindow(PowerToysRunSettings settings, MainViewModel mainVM)
            : this()
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;

            InitializeComponent();

            _firstDeleteTimer.Elapsed += CheckForFirstDelete;
            _firstDeleteTimer.Interval = 1000;
            NativeEventWaiter.WaitForEventLoop(Constants.RunSendSettingsTelemetryEvent(), SendSettingsTelemetry);
        }

        private void SendSettingsTelemetry()
        {
            try
            {
                Log.Info("Send Run settings telemetry", this.GetType());
                var plugins = PluginManager.AllPlugins.ToDictionary(x => x.Metadata.Name + " " + x.Metadata.ID, x => new PluginModel()
                {
                    ID = x.Metadata.ID,
                    Name = x.Metadata.Name,
                    Disabled = x.Metadata.Disabled,
                    ActionKeyword = x.Metadata.ActionKeyword,
                    IsGlobal = x.Metadata.IsGlobal,
                });

                var telemetryEvent = new RunPluginsSettingsEvent(plugins);
                PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
            }
            catch (Exception ex)
            {
                Log.Exception("Unhandled exception when trying to send PowerToys Run settings telemetry.", ex, GetType());
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowsInteropHelper.SetToolWindowStyle(this);
        }

        private void CheckForFirstDelete(object sender, ElapsedEventArgs e)
        {
            if (_firstDeleteTimer != null)
            {
                _firstDeleteTimer.Stop();
                if (_deletePressed)
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherFirstDeleteEvent());
                }
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

        private void BringProcessToForeground()
        {
            // Use SendInput hack to allow Activate to work - required to resolve focus issue https://github.com/microsoft/PowerToys/issues/4270
            WindowsInteropHelper.INPUT input = new WindowsInteropHelper.INPUT { Type = WindowsInteropHelper.INPUTTYPE.INPUTMOUSE, Data = { } };
            WindowsInteropHelper.INPUT[] inputs = new WindowsInteropHelper.INPUT[] { input };

            // Send empty mouse event. This makes this thread the last to send input, and hence allows it to pass foreground permission checks
            _ = NativeMethods.SendInput(1, inputs, WindowsInteropHelper.INPUT.Size);
            Activate();
        }

        private const string EnvironmentChangeType = "Environment";

#pragma warning disable CA1801 // Review unused parameters
        public IntPtr ProcessWindowMessages(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
#pragma warning restore CA1801 // Review unused parameters
        {
            switch ((WM)msg)
            {
                case WM.SETTINGCHANGE:
                    string changeType = Marshal.PtrToStringUni(lparam);
                    if (changeType == EnvironmentChangeType)
                    {
                        Log.Info("Reload environment: Updating environment variables for PT Run's process", typeof(EnvironmentHelper));
                        EnvironmentHelper.UpdateEnvironment();
                        handled = true;
                    }

                    break;
                case WM.HOTKEY:
                    handled = _viewModel.ProcessHotKeyMessages(wparam, lparam);
                    break;
            }

            return IntPtr.Zero;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            // Initialize protected environment variables before register the WindowMessage
            EnvironmentHelper.GetProtectedEnvironmentVariables();

            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource.AddHook(ProcessWindowMessages);

            // Call RegisterHotKey only after a window handle can be used, so that a global hotkey can be registered.
            _viewModel.RegisterHotkey(_hwndSource.Handle);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowsInteropHelper.DisableControlBox(this);
            InitializePosition();

            SearchBox.QueryTextBox.DataContext = _viewModel;
            SearchBox.QueryTextBox.PreviewKeyDown += Launcher_KeyDown;

            SetupSearchTextBoxReactiveness(_viewModel.GetSearchQueryResultsWithDelaySetting());
            _viewModel.RegisterSettingsChangeListener(
                (s, prop_e) =>
                {
                    if (prop_e.PropertyName == nameof(PowerToysRunSettings.SearchQueryResultsWithDelay) || prop_e.PropertyName == nameof(PowerToysRunSettings.SearchInputDelay) || prop_e.PropertyName == nameof(PowerToysRunSettings.SearchInputDelayFast))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SetupSearchTextBoxReactiveness(_viewModel.GetSearchQueryResultsWithDelaySetting());
                        });
                    }
                });

            // Set initial language flow direction
            SearchBox_UpdateFlowDirection();

            // Register language changed event
            InputLanguageManager.Current.InputLanguageChanged += SearchBox_InputLanguageChanged;

            SearchBox.QueryTextBox.Focus();
            SearchBox.QueryTextBox.ControlledElements.Add(ListBox.SuggestionsList);

            ListBox.DataContext = _viewModel;
            ListBox.SuggestionsList.SelectionChanged += SuggestionsList_SelectionChanged;
            ListBox.SuggestionsList.PreviewMouseLeftButtonUp += SuggestionsList_PreviewMouseLeftButtonUp;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.MainWindowVisibility = Visibility.Collapsed;
            _viewModel.LoadedAtLeastOnce = true;

            BringProcessToForeground();
        }

        private void SetupSearchTextBoxReactiveness(bool showResultsWithDelay)
        {
            if (_reactiveSubscription != null)
            {
                _reactiveSubscription.Dispose();
                _reactiveSubscription = null;
            }

            SearchBox.QueryTextBox.TextChanged -= QueryTextBox_TextChanged;

            if (showResultsWithDelay)
            {
                _reactiveSubscription = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                    add => SearchBox.QueryTextBox.TextChanged += add,
                    remove => SearchBox.QueryTextBox.TextChanged -= remove)
                    .Do(@event => ClearAutoCompleteText((TextBox)@event.Sender))
                    .Throttle(TimeSpan.FromMilliseconds(_settings.SearchInputDelayFast))
                    .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, false)))
                    .Throttle(TimeSpan.FromMilliseconds(_settings.SearchInputDelay))
                    .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, true)))
                    .Subscribe();

                /*
                if (_settings.PTRSearchQueryFastResultsWithDelay)
                {
                    // old mode, delay fast and delayed execution
                    _reactiveSubscription = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                        add => SearchBox.QueryTextBox.TextChanged += add,
                        remove => SearchBox.QueryTextBox.TextChanged -= remove)
                        .Do(@event => ClearAutoCompleteText((TextBox)@event.Sender))
                        .Throttle(TimeSpan.FromMilliseconds(searchInputDelayMs))
                        .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender)))
                        .Subscribe();
                }
                else
                {
                    if (_settings.PTRSearchQueryFastResultsWithPartialDelay)
                    {
                        // new mode, fire non-delayed right away, and then later the delayed execution
                        _reactiveSubscription = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                            add => SearchBox.QueryTextBox.TextChanged += add,
                            remove => SearchBox.QueryTextBox.TextChanged -= remove)
                            .Do(@event => ClearAutoCompleteText((TextBox)@event.Sender))
                            .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, false)))
                            .Throttle(TimeSpan.FromMilliseconds(searchInputDelayMs))
                            .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, true)))
                            .Subscribe();
                    }
                    else
                    {
                        // new mode, fire non-delayed after short delay, and then later the delayed execution
                        _reactiveSubscription = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                            add => SearchBox.QueryTextBox.TextChanged += add,
                            remove => SearchBox.QueryTextBox.TextChanged -= remove)
                            .Do(@event => ClearAutoCompleteText((TextBox)@event.Sender))
                            .Throttle(TimeSpan.FromMilliseconds(_settings.SearchInputDelayFast))
                            .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, false)))
                            .Throttle(TimeSpan.FromMilliseconds(searchInputDelayMs))
                            .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, true)))
                            .Subscribe();
                    }
                }
                */
            }
            else
            {
                SearchBox.QueryTextBox.TextChanged += QueryTextBox_TextChanged;
            }
        }

        private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var result = ((FrameworkElement)e.OriginalSource).DataContext;
            if (result != null)
            {
                // This may be null if the tapped item was one of the context buttons (run as admin etc).
                if (result is ResultViewModel resultVM)
                {
                    _viewModel.Results.SelectedItem = resultVM;
                    _viewModel.OpenResultWithMouseCommand.Execute(null);
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.MainWindowVisibility))
            {
                if (Visibility == System.Windows.Visibility.Visible && _viewModel.MainWindowVisibility != Visibility.Hidden)
                {
                    // Not called on first launch
                    // Called when window is made visible by hotkey. Not called when the window is deactivated by clicking away
                    UpdatePosition();
                    BringProcessToForeground();

                    // HACK: Setting focus here again fixes some focus issues, like on first run or after showing a message box.
                    SearchBox.QueryTextBox.Focus();
                    Keyboard.Focus(SearchBox.QueryTextBox);

                    if (!_viewModel.LastQuerySelected)
                    {
                        _viewModel.LastQuerySelected = true;
                    }
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.SystemQueryText))
            {
                _isTextSetProgrammatically = true;
                if (_viewModel.Results != null)
                {
                    SearchBox.QueryTextBox.Text = MainViewModel.GetSearchText(
                        _viewModel.Results.SelectedIndex,
                        _viewModel.SystemQueryText,
                        _viewModel.QueryText);
                }
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
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
            if (_settings.HideWhenDeactivated)
            {
                // (this.FindResource("OutroStoryboard") as Storyboard).Begin();
                _viewModel.Hide();
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
                Top = WindowTop();
                Left = WindowLeft();
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
            var screen = GetScreen();
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = ((dip2.X - ActualWidth) / 2) + dip1.X;
            return left;
        }

        private double WindowTop()
        {
            var screen = GetScreen();
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = ((dip2.Y - SearchBox.ActualHeight) / 4) + dip1.Y;
            return top;
        }

        private Screen GetScreen()
        {
            ManagedCommon.StartupPosition position = _settings.StartupPosition;
            switch (position)
            {
                case ManagedCommon.StartupPosition.PrimaryMonitor:
                    return Screen.PrimaryScreen;
                case ManagedCommon.StartupPosition.Focus:
                    IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();
                    Screen activeScreen = Screen.FromHandle(foregroundWindowHandle);
                    return activeScreen;
                case ManagedCommon.StartupPosition.Cursor:
                default:
                    return Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            }
        }

        private void Launcher_KeyDown(object sender, KeyEventArgs e)
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
            else if (e.Key == Key.Right)
            {
                if (SearchBox.QueryTextBox.CaretIndex == SearchBox.QueryTextBox.Text.Length)
                {
                    _viewModel.SelectNextContextMenuItemCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Left)
            {
                if (SearchBox.QueryTextBox.CaretIndex == SearchBox.QueryTextBox.Text.Length)
                {
                    if (_viewModel.Results != null && _viewModel.Results.IsContextMenuItemSelected())
                    {
                        _viewModel.SelectPreviousContextMenuItemCommand.Execute(null);
                        e.Handled = true;
                    }
                }
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
            else if (e.Key == Key.Back)
            {
                _deletePressed = true;
            }
            else
            {
                _viewModel.HandleContextMenu(e.Key, Keyboard.Modifiers);
            }
        }

        private void UpdateTextBoxToSelectedItem()
        {
            var itemText = _viewModel?.Results?.SelectedItem?.SearchBoxDisplayText() ?? null;
            if (!string.IsNullOrEmpty(itemText))
            {
                _viewModel.ChangeQueryText(itemText);
            }
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listview = (ListView)sender;
            _viewModel.Results.SelectedItem = (ResultViewModel)listview.SelectedItem;
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                try
                {
                    listview.ScrollIntoView(e.AddedItems[0]);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    // Due to virtualization being enabled for the listview, the layout system updates elements in a deferred manner using an algorithm that balances performance and concurrency.
                    // Hence, there can be a situation where the element index that we want to scroll into view is out of range for it's parent control.
                    // To mitigate this we use the UpdateLayout function, which forces layout update to ensure that the parent element contains the latest properties.
                    // However, it has a performance impact and is therefore not called each time.
                    Log.Exception("The parent element layout is not updated yet", ex, GetType());
                    listview.UpdateLayout();
                    listview.ScrollIntoView(e.AddedItems[0]);
                }
            }

            // To populate the AutoCompleteTextBox as soon as the selection is changed or set.
            // Setting it here instead of when the text is changed as there is a delay in executing the query and populating the result
            if (_viewModel.Results != null && !string.IsNullOrEmpty(SearchBox.QueryTextBox.Text))
            {
                SearchBox.AutoCompleteTextBlock.Text = MainViewModel.GetAutoCompleteText(
                    _viewModel.Results.SelectedIndex,
                    _viewModel.Results.SelectedItem?.SearchBoxDisplayText(),
                    _viewModel.QueryText);
            }
        }

        private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            ClearAutoCompleteText(textBox);
            PerformSearchQuery(textBox);
        }

        private void ClearAutoCompleteText(TextBox textBox)
        {
            var text = textBox.Text;
            var autoCompleteText = SearchBox.AutoCompleteTextBlock.Text;

            if (MainViewModel.ShouldAutoCompleteTextBeEmpty(text, autoCompleteText))
            {
                SearchBox.AutoCompleteTextBlock.Text = string.Empty;
            }

            var showResultsWithDelay = _viewModel.GetSearchQueryResultsWithDelaySetting();

            // only if we are using throttled search and throttled 'fast' search, do we need to do anything different with the current results.
            if (showResultsWithDelay && _settings.PTRSearchQueryFastResultsWithDelay)
            {
                // Default means we don't do anything we did not do before... leave the results as is, they will be changed as needed when results are returned
                var pTRunStartNewSearchAction = _settings.PTRunStartNewSearchAction ?? "Default";

                if (pTRunStartNewSearchAction == "DeSelect")
                {
                    // leave the results, be deselect anything to it will not be activated by <enter> key, can still be arrow-key or clicked though
                    if (!_isTextSetProgrammatically)
                    {
                        DeselectAllResults();
                    }
                }
                else if (pTRunStartNewSearchAction == "Clear")
                {
                    // remove all results to prepare for new results, this causes flashing usually and is not cool
                    if (!_isTextSetProgrammatically)
                    {
                        ClearResults();
                    }
                }
            }
        }

        private void ClearResults()
        {
            _viewModel.Results.SelectedItem = null;
            System.Threading.Tasks.Task.Run(() =>
            {
                Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    _viewModel.Results.Clear();
                    _viewModel.Results.Results.NotifyChanges();
                }));
            });
        }

        private void DeselectAllResults()
        {
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                _viewModel.Results.SelectedIndex = -1;
            }));
        }

        private void PerformSearchQuery(TextBox textBox)
        {
            PerformSearchQuery(textBox, null);
        }

        private void PerformSearchQuery(TextBox textBox, bool? delayedExecution)
        {
            var text = textBox.Text;

            if (_isTextSetProgrammatically)
            {
                textBox.SelectionStart = textBox.Text.Length;

                // because IF this is delayedExecution = false (run fast queries) we know this will be called again with delayedExecution = true
                // if we don't do this, the second (partner) call will not be called _isTextSetProgrammatically = true also, and we need it to.
                if (delayedExecution.HasValue && delayedExecution.Value)
                {
                    _isTextSetProgrammatically = false;
                }
            }
            else
            {
                _viewModel.QueryText = text;
                _viewModel.Query(delayedExecution);
            }
        }

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;
            }
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                _deletePressed = false;
                if (_firstDeleteTimer != null)
                {
                    _firstDeleteTimer.Start();
                }

                // (this.FindResource("IntroStoryboard") as Storyboard).Begin();
                SearchBox.QueryTextBox.Focus();
                Keyboard.Focus(SearchBox.QueryTextBox);

                _settings.ActivateTimes++;

                if (!string.IsNullOrEmpty(SearchBox.QueryTextBox.Text))
                {
                    SearchBox.QueryTextBox.SelectAll();
                }

                // Log the time taken from pressing the hotkey till launcher is visible as separate events depending on if it's the first hotkey invoke or second
                if (!_coldStateHotkeyPressed)
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherColdStateHotkeyEvent() { HotkeyToVisibleTimeMs = _viewModel.GetHotkeyEventTimeMs() });
                    _coldStateHotkeyPressed = true;
                }
                else
                {
                    PowerToysTelemetry.Log.WriteEvent(new LauncherWarmStateHotkeyEvent() { HotkeyToVisibleTimeMs = _viewModel.GetHotkeyEventTimeMs() });
                }
            }
            else
            {
                if (_firstDeleteTimer != null)
                {
                    _firstDeleteTimer.Stop();
                }
            }
        }

        private void OutroStoryboard_Completed(object sender, EventArgs e)
        {
            Hide();
        }

        private void SearchBox_UpdateFlowDirection()
        {
            SearchBox.QueryTextBox.FlowDirection = MainViewModel.GetLanguageFlowDirection();
            SearchBox.AutoCompleteTextBlock.FlowDirection = MainViewModel.GetLanguageFlowDirection();
        }

        private void SearchBox_InputLanguageChanged(object sender, InputLanguageEventArgs e)
        {
            SearchBox_UpdateFlowDirection();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_firstDeleteTimer != null)
                    {
                        _firstDeleteTimer.Dispose();
                    }

                    _hwndSource?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _firstDeleteTimer = null;
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MainWindow()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _hwndSource.RemoveHook(ProcessWindowMessages);
            _hwndSource = null;
        }
    }
}
