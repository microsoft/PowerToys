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
using System.Windows.Media.Imaging;

using Common.UI;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.Telemetry.Events;
using PowerLauncher.ViewModel;
using PowerToys.Interop;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Interfaces;

using CancellationToken = System.Threading.CancellationToken;
using Image = Wox.Infrastructure.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Log = Wox.Plugin.Logger.Log;
using Screen = System.Windows.Forms.Screen;

namespace PowerLauncher
{
    public partial class MainWindow : IDisposable
    {
        private readonly PowerToysRunSettings _settings;
        private readonly MainViewModel _viewModel;
        private readonly CancellationToken _nativeWaiterCancelToken;
        private bool _isTextSetProgrammatically;
        private bool _deletePressed;
        private HwndSource _hwndSource;
        private Timer _firstDeleteTimer = new Timer();
        private bool _coldStateHotkeyPressed;
        private bool _disposedValue;
        private IDisposable _reactiveSubscription;
        private Point _mouseDownPosition;
        private ResultViewModel _mouseDownResultViewModel;

        // The enum flag for DwmSetWindowAttribute's second parameter, which tells the function what attribute to set.
        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
        }

        // The DWM_WINDOW_CORNER_PREFERENCE enum for DwmSetWindowAttribute's third parameter, which tells the function
        // what value of the enum to set.
        // Copied from dwmapi.h
        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3,
        }

        // Import dwmapi.dll and define DwmSetWindowAttribute in C# corresponding to the native function.
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(
            IntPtr hwnd,
            DWMWINDOWATTRIBUTE attribute,
            ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
            uint cbAttribute);

        public MainWindow(PowerToysRunSettings settings, MainViewModel mainVM, CancellationToken nativeWaiterCancelToken)
            : this()
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _nativeWaiterCancelToken = nativeWaiterCancelToken;
            _settings = settings;

            // Fixes #30850
            AppContext.SetSwitch("Switch.System.Windows.Controls.Grid.StarDefinitionsCanExceedAvailableSpace", true);

            InitializeComponent();

            _firstDeleteTimer.Elapsed += CheckForFirstDelete;
            _firstDeleteTimer.Interval = 1000;
            NativeEventWaiter.WaitForEventLoop(
                Constants.RunSendSettingsTelemetryEvent(),
                SendSettingsTelemetry,
                Application.Current.Dispatcher,
                _nativeWaiterCancelToken);
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

        public IntPtr ProcessWindowMessages(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
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
            if (OSVersionHelper.IsGreaterThanWindows11_21H2())
            {
                // ResizeMode="NoResize" removes rounded corners. So force them to rounded.
                IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
                DWMWINDOWATTRIBUTE attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
                DWM_WINDOW_CORNER_PREFERENCE preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));
            }
            else
            {
                // On Windows10 ResizeMode="NoResize" removes the border so we add a new one.
                // Also on 22000 it crashes due to DWMWA_WINDOW_CORNER_PREFERENCE https://github.com/microsoft/PowerToys/issues/36558
                MainBorder.BorderThickness = new System.Windows.Thickness(0.5);
            }
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
            ListBox.SuggestionsList.PreviewMouseLeftButtonDown += SuggestionsList_PreviewMouseLeftButtonDown;
            ListBox.SuggestionsList.MouseMove += SuggestionsList_MouseMove;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.MainWindowVisibility = Visibility.Collapsed;
            _viewModel.LoadedAtLeastOnce = true;
            _viewModel.SetPluginsOverviewVisibility();
            _viewModel.SetFontSize();

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
                _reactiveSubscription = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventWithInitiatorArgs>(
                    conversion => (sender, eventArg) => conversion(sender, new TextChangedEventWithInitiatorArgs(eventArg.RoutedEvent, eventArg.UndoAction)),
                    add => SearchBox.QueryTextBox.TextChanged += add,
                    remove => SearchBox.QueryTextBox.TextChanged -= remove)
                    .Do(@event => ClearAutoCompleteText((TextBox)@event.Sender, @event))
                    .Throttle(TimeSpan.FromMilliseconds(_settings.SearchInputDelayFast))
                    .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, false, @event)))
                    .Throttle(TimeSpan.FromMilliseconds(_settings.SearchInputDelay))
                    .Do(@event => Dispatcher.InvokeAsync(() => PerformSearchQuery((TextBox)@event.Sender, true, @event)))
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

        private void SuggestionsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPosition = e.GetPosition(null);
            _mouseDownResultViewModel = ((FrameworkElement)e.OriginalSource).DataContext as ResultViewModel;
        }

        private void SuggestionsList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _mouseDownResultViewModel?.Result?.ContextData is IFileDropResult fileDropResult)
            {
                Vector dragDistance = _mouseDownPosition - e.GetPosition(null);
                if (Math.Abs(dragDistance.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(dragDistance.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _viewModel.Hide();

                    try
                    {
                        // DoDragDrop with file thumbnail as drag image
                        var dataObject = DragDataObject.FromFile(fileDropResult.Path);
                        using var bitmap = DragDataObject.BitmapSourceToBitmap((BitmapSource)_mouseDownResultViewModel?.Image);
                        IntPtr hBitmap = bitmap.GetHbitmap();

                        try
                        {
                            dataObject.SetDragImage(hBitmap, Constant.ThumbnailSize, Constant.ThumbnailSize);
                            DragDrop.DoDragDrop(ListBox.SuggestionsList, dataObject, DragDropEffects.Copy);
                        }
                        finally
                        {
                            Image.NativeMethods.DeleteObject(hBitmap);
                        }
                    }
                    catch
                    {
                        // DoDragDrop without drag image
                        IDataObject dataObject = new DataObject(DataFormats.FileDrop, new[] { fileDropResult.Path });
                        DragDrop.DoDragDrop(ListBox.SuggestionsList, dataObject, DragDropEffects.Copy);
                    }
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

                    _viewModel.SetPluginsOverviewVisibility();
                    _viewModel.SetFontSize();

                    if (_viewModel.Plugins.Count > 0)
                    {
                        _viewModel.SelectedPlugin = null;
                        pluginsHintsList.ScrollIntoView(pluginsHintsList.Items[0]);
                    }

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
                    string newText = MainViewModel.GetSearchText(
                        _viewModel.Results.SelectedIndex,
                        _viewModel.SystemQueryText,
                        _viewModel.QueryText);
                    if (SearchBox.QueryTextBox.Text != newText)
                    {
                        SearchBox.QueryTextBox.Text = newText;
                    }
                    else
                    {
                        _isTextSetProgrammatically = false;
                    }
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
            MoveToDesiredPosition();

            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_settings.HideWhenDeactivated)
            {
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
                MoveToDesiredPosition();
            }
        }

        private void MoveToDesiredPosition()
        {
            // Hack: After switching to PerMonitorV2, this operation seems to require a three-step operation
            // to ensure a stable position: First move to top-left of desired screen, then centralize twice.
            // More straightforward ways of doing this don't seem to work well for unclear reasons, but possibly related to
            // https://github.com/dotnet/wpf/issues/4127
            // In any case, there does not seem to be any big practical downside to doing it this way. As a bonus, it can be
            // done in pure WPF without any native calls and without too much DPI-based fiddling.
            // In terms of the hack itself, removing any of these three steps seems to fail in certain scenarios only,
            // so be careful with testing!
            var desiredScreen = GetScreen();
            var workingArea = desiredScreen.WorkingArea;
            Point ToDIP(double unitX, double unitY) => WindowsInteropHelper.TransformPixelsToDIP(this, unitX, unitY);

            // Move to top-left of desired screen.
            Top = workingArea.Top;
            Left = workingArea.Left;

            // Centralize twice.
            void MoveToScreenTopCenter()
            {
                Left = ((ToDIP(workingArea.Width, 0).X - ActualWidth) / 2) + ToDIP(workingArea.X, 0).X;
                Top = ((ToDIP(0, workingArea.Height).Y - SearchBox.ActualHeight) / 4) + ToDIP(0, workingArea.Y).Y;
            }

            MoveToScreenTopCenter();
            MoveToScreenTopCenter();
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_settings != null && _settings.RememberLastLaunchLocation)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
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
            if (_viewModel.PluginsOverviewVisibility == Visibility.Visible)
            {
                if (e.Key == Key.Up)
                {
                    _viewModel.SelectPrevOverviewPluginCommand.Execute(null);
                    pluginsHintsList.ScrollIntoView(_viewModel.SelectedPlugin);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    _viewModel.SelectNextOverviewPluginCommand.Execute(null);
                    pluginsHintsList.ScrollIntoView(_viewModel.SelectedPlugin);
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                {
                    _viewModel.SelectPrevOverviewPluginCommand.Execute(null);
                    pluginsHintsList.ScrollIntoView(_viewModel.SelectedPlugin);
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab)
                {
                    _viewModel.SelectNextOverviewPluginCommand.Execute(null);
                    pluginsHintsList.ScrollIntoView(_viewModel.SelectedPlugin);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    QueryForSelectedPlugin();
                    e.Handled = true;
                }
            }
            else
            {
                if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
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
                    // Hence, there can be a situation where the element index that we want to scroll into view is out of range for its parent control.
                    // To mitigate this we use the UpdateLayout function, which forces layout update to ensure that the parent element contains the latest properties.
                    // However, it has a performance impact and is therefore not called each time.
                    Log.Exception("The parent element layout is not updated yet", ex, GetType());
                    listview.UpdateLayout();
                    listview.ScrollIntoView(e.AddedItems[0]);
                }
            }

            // To populate the AutoCompleteTextBox as soon as the selection is changed or set.
            // Setting it here instead of when the text is changed as there is a delay in executing the query and populating the result
            if (!string.IsNullOrEmpty(SearchBox.QueryTextBox.Text))
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
            ClearAutoCompleteText(textBox, null);
            PerformSearchQuery(textBox);
        }

        private void ClearAutoCompleteText(TextBox textBox, System.Reactive.EventPattern<TextChangedEventWithInitiatorArgs> @event)
        {
            bool isTextSetProgrammaticallyAtStart = _isTextSetProgrammatically;
            if (@event != null)
            {
                @event.EventArgs.IsTextSetProgrammatically = isTextSetProgrammaticallyAtStart;
            }

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
                    if (!isTextSetProgrammaticallyAtStart)
                    {
                        DeselectAllResults();
                    }
                }
                else if (pTRunStartNewSearchAction == "Clear")
                {
                    // remove all results to prepare for new results, this causes flashing usually and is not cool
                    if (!isTextSetProgrammaticallyAtStart)
                    {
                        ClearResults();
                    }
                }
            }
        }

        private void ClearResults()
        {
            MainViewModel.PerformSafeAction(() =>
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
            PerformSearchQuery(textBox, null, null);
        }

        private void PerformSearchQuery(TextBox textBox, bool? delayedExecution, System.Reactive.EventPattern<TextChangedEventWithInitiatorArgs> @event)
        {
            var text = textBox.Text;
            bool isTextSetProgrammaticallyForEvent = _isTextSetProgrammatically;

            if (@event != null)
            {
                isTextSetProgrammaticallyForEvent = @event.EventArgs.IsTextSetProgrammatically;
            }

            if (isTextSetProgrammaticallyForEvent)
            {
                textBox.SelectionStart = textBox.Text.Length;

                // because IF this is delayedExecution = false (run fast queries) we know this will be called again with delayedExecution = true
                // if we don't do this, the second (partner) call will not be called _isTextSetProgrammatically = true also, and we need it to.
                // Also, if search query delay is disabled, second call won't come, so reset _isTextSetProgrammatically anyway
                if ((delayedExecution.HasValue && delayedExecution.Value) || !_viewModel.GetSearchQueryResultsWithDelaySetting())
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
                    _firstDeleteTimer?.Dispose();
                    _hwndSource?.Dispose();
                }

                _firstDeleteTimer = null;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                _hwndSource.RemoveHook(ProcessWindowMessages);
            }
            catch (Exception ex)
            {
                Log.Exception($"Exception when trying to Remove hook", ex, ex.GetType());
            }

            _hwndSource = null;
        }

        private void PluginsHintsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            QueryForSelectedPlugin();
        }

        private void QueryForSelectedPlugin()
        {
            if (_viewModel.Plugins.Count > 0 && _viewModel.SelectedPlugin != null)
            {
                _viewModel.ChangeQueryText(_viewModel.SelectedPlugin.Metadata.ActionKeyword, true);
                SearchBox.QueryTextBox.Focus();

                _viewModel.SelectedPlugin = null;
                pluginsHintsList.ScrollIntoView(pluginsHintsList.Items[0]);
            }
        }
    }
}
