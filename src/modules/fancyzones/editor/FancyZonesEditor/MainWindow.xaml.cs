// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using FancyZoneEditor.Telemetry;
using FancyZonesEditor.Helpers;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using FancyZonesEditor.ViewModels;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;
using WinUIEx;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private const int MinimalForDefaultWrapPanelsHeight = 900;
        private const int DefaultWrapPanelItemSize = 164;
        private const int SmallWrapPanelItemSize = 164;

        private static CompositeFormat _editTemplate;
        private static CompositeFormat _pixelValue;
        private static CompositeFormat _templateZoneCountValue;

        private readonly MainWindowSettingsModel _settings = ((App)Application.Current).MainWindowSettings;
        private readonly MonitorViewModel _monitorViewModel = new MonitorViewModel();
        private readonly Rect _workArea;
        private readonly bool _spanZonesAcrossMonitors;

        private ContentDialog _openedDialog;
        private bool _haveTriedToGetFocusAlready;

        public MainWindow(bool spanZonesAcrossMonitors, Rect workArea)
        {
            InitializeComponent();

            _spanZonesAcrossMonitors = spanZonesAcrossMonitors;
            _workArea = workArea;

            Title = ResourceLoaderInstance.GetString("Fancy_Zones_Editor_App_Title");
            RootGrid.DataContext = _settings;

            Monitors.ItemsSource = _monitorViewModel.MonitorInfoForViewModel;
            TemplatesGridView.ItemsSource = MainWindowSettingsModel.TemplateModels;
            CustomGridView.ItemsSource = MainWindowSettingsModel.CustomModels;

            MainWindowSettingsModel.CustomModels.CollectionChanged += CustomModels_CollectionChanged;
            UpdateEmptyCustomLayoutsMessage();

            // WinUI cannot x:Bind to a named element from a Window-rooted XAML tree, so the
            // accessibility labels the WPF markup declared are wired up here instead.
            AutomationProperties.SetLabeledBy(TemplatesGridView, TemplatesHeaderBlock);
            AutomationProperties.SetLabeledBy(CustomGridView, CustomHeaderBlock);
            AutomationProperties.SetLabeledBy(QuickKeySelectionComboBox, QuickKeyTitle);
            AutomationProperties.SetLabeledBy(LayoutNameText, NameHeaderText);

            RootGrid.KeyUp += MainWindow_KeyUp;
            RootGrid.KeyDown += MainWindow_KeyDown;
            RootGrid.Loaded += RootGrid_Loaded;

            Closed += OnClosing;

            if (workArea.Height < MinimalForDefaultWrapPanelsHeight || App.Overlay.MultiMonitorMode)
            {
                WrapPanelItemSize = SmallWrapPanelItemSize;
            }

            // reinit considering work area rect
            _settings.InitModels();

            PowerToysTelemetry.Log.WriteEvent(new FancyZonesEditorStartFinishEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        public int WrapPanelItemSize { get; set; } = DefaultWrapPanelItemSize;

        // ResourceLoader is not available at class-load time in every host, so
        // resource-dependent statics are resolved lazily.
        private static CompositeFormat EditTemplate => _editTemplate ??=
            CompositeFormat.Parse(ResourceLoaderInstance.GetString("Edit_Template"));

        private static CompositeFormat PixelValue => _pixelValue ??=
            CompositeFormat.Parse(ResourceLoaderInstance.GetString("Pixel_Value"));

        private static CompositeFormat TemplateZoneCountValue => _templateZoneCountValue ??=
            CompositeFormat.Parse(ResourceLoaderInstance.GetString("Template_Zone_Count_Value"));

        /// <summary>
        /// Makes this window an owned window of the layout overlay, so it stays on top of it.
        /// WinUI 3 has no <c>Window.Owner</c>.
        /// </summary>
        /// <param name="ownerHwnd">Handle of the layout overlay window.</param>
        public void SetOwner(IntPtr ownerHwnd)
        {
            NativeMethods.SetWindowOwner(WindowNative.GetWindowHandle(this), ownerHwnd);
        }

        public void Update()
        {
            RootGrid.DataContext = null;
            RootGrid.DataContext = _settings;
        }

        private static bool IsCtrlKeyDown()
        {
            return Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            SizeToContent();
            PositionWindow();

            if (!_haveTriedToGetFocusAlready)
            {
                BringWindowToFront();
            }
        }

        /// <summary>
        /// WinUI 3 has no <c>SizeToContent</c>; the window is measured and resized to the content,
        /// clamped to the work area the way the WPF MaxWidth/MaxHeight did.
        /// </summary>
        private void SizeToContent()
        {
            RootGrid.Measure(new Size(_workArea.Width, _workArea.Height));
            Size desired = RootGrid.DesiredSize;

            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            int width = (int)Math.Ceiling(Math.Min(desired.Width, _workArea.Width) * scale);
            int height = (int)Math.Ceiling(Math.Min(desired.Height, _workArea.Height) * scale);

            AppWindow.ResizeClient(new SizeInt32(Math.Max(width, 1), Math.Max(height, 1)));
        }

        private void PositionWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            // Move onto the target monitor first (virtual coordinates, matching how the overlay
            // is positioned), then center within that monitor's work area.
            NativeMethods.SetWindowPositionDpiUnaware(hwnd, (int)_workArea.X, (int)_workArea.Y, AppWindow.Size.Width, AppWindow.Size.Height);

            var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            RectInt32 area = display.WorkArea;
            AppWindow.Move(new PointInt32(
                area.X + ((area.Width - AppWindow.Size.Width) / 2),
                area.Y + ((area.Height - AppWindow.Size.Height) / 2)));
        }

        private void BringWindowToFront()
        {
            // Get the window handle of the FancyZones Editor window
            IntPtr handle = WindowNative.GetWindowHandle(this);

            // Get the handle of the window currently in the foreground
            IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();

            // Get the thread IDs of the current thread and the thread of the foreground window
            uint currentThreadId = NativeMethods.GetCurrentThreadId();
            uint activeThreadId = NativeMethods.GetWindowThreadProcessId(foregroundWindowHandle, IntPtr.Zero);

            // Check if the active thread is different from the current thread
            if (activeThreadId != currentThreadId)
            {
                // Attach the input processing mechanism of the current thread to the active thread
                NativeMethods.AttachThreadInput(activeThreadId, currentThreadId, true);

                // Set the FancyZones Editor window as the foreground window
                NativeMethods.SetForegroundWindow(handle);

                // Detach the input processing mechanism of the current thread from the active thread
                NativeMethods.AttachThreadInput(activeThreadId, currentThreadId, false);
            }
            else
            {
                // Set the FancyZones Editor window as the foreground window
                NativeMethods.SetForegroundWindow(handle);
            }

            // Bring the FancyZones Editor window to the foreground and activate it
            NativeMethods.SwitchToThisWindow(handle, true);

            _haveTriedToGetFocusAlready = true;
        }

        private void CustomModels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateEmptyCustomLayoutsMessage();
        }

        private void UpdateEmptyCustomLayoutsMessage()
        {
            NoCustomLayoutsPanel.Visibility = MainWindowSettingsModel.CustomModelsCount == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void MainWindow_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                CloseDialog();
            }
        }

        // Prevent closing the dialog with enter
        private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && _openedDialog != null)
            {
                if (e.OriginalSource is RadioButton source && source.IsChecked != true)
                {
                    source.IsChecked = true;
                    e.Handled = true;
                }
            }
        }

        private void LayoutItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            CloseDialog();
        }

        private void CloseDialog()
        {
            if (_openedDialog != null)
            {
                _openedDialog.Hide();
            }
            else
            {
                OnClosing(this, null);
            }
        }

        private void Select(LayoutModel newSelection)
        {
            _settings.SetSelectedModel(newSelection);
            App.Overlay.CurrentDataContext = newSelection;
        }

        private async void NewLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            if (_openedDialog != null)
            {
                // another dialog already opened
                return;
            }

            string defaultNamePrefix = ResourceLoaderInstance.GetString("Default_Custom_Layout_Name");
            int maxCustomIndex = 0;
            foreach (LayoutModel customModel in MainWindowSettingsModel.CustomModels)
            {
                string name = customModel.Name;
                if (name != null && name.StartsWith(defaultNamePrefix, StringComparison.CurrentCulture))
                {
                    if (int.TryParse(name.AsSpan(defaultNamePrefix.Length), out int i))
                    {
                        if (maxCustomIndex < i)
                        {
                            maxCustomIndex = i;
                        }
                    }
                }
            }

            LayoutNameText.Text = defaultNamePrefix + " " + (++maxCustomIndex);

            GridLayoutRadioButton.IsChecked = true;
            CanvasLayoutRadioButton.IsChecked = false;
            GridLayoutRadioButton.Focus(FocusState.Programmatic);

            NewLayoutDialog.XamlRoot = RootGrid.XamlRoot;
            await NewLayoutDialog.ShowAsync();
        }

        private void LayoutNameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Replaces the WPF binding on Text.Length, which WinUI cannot convert to a bool.
            NewLayoutDialog.IsPrimaryButtonEnabled = LayoutNameText.Text.Length > 0;
        }

        private void DuplicateLayout_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            var dataContext = ((FrameworkElement)sender).DataContext;
            EditLayoutDialog.Hide();

            if (dataContext is not LayoutModel model)
            {
                return;
            }

            // make a copy
            model = model.Clone();

            string name = model.Name;
            var index = name.LastIndexOf('(');
            if (index != -1)
            {
                name = name.Remove(index);
                name = name.TrimEnd();
            }

            Announce(name, ResourceLoaderInstance.GetString("Layout_Creation_Announce"));
            int maxCustomIndex = 0;
            foreach (LayoutModel customModel in MainWindowSettingsModel.CustomModels)
            {
                string customModelName = customModel.Name;
                if (customModelName.StartsWith(name, StringComparison.CurrentCulture))
                {
                    int openBraceIndex = customModelName.LastIndexOf('(');
                    int closeBraceIndex = customModelName.LastIndexOf(')');
                    if (openBraceIndex != -1 && closeBraceIndex != -1)
                    {
                        string indexSubstring = customModelName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                        if (int.TryParse(indexSubstring, out int i))
                        {
                            if (maxCustomIndex < i)
                            {
                                maxCustomIndex = i;
                            }
                        }
                    }
                }
            }

            model.Name = name + " (" + (++maxCustomIndex) + ')';
            model.Persist();

            App.FancyZonesEditorIO.SerializeCustomLayouts();
        }

        private void Announce(string name, string message)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.MenuOpened) && LayoutCreationAnnounce != null)
            {
                var peer = FrameworkElementAutomationPeer.FromElement(LayoutCreationAnnounce);
                AutomationProperties.SetName(LayoutCreationAnnounce, name + " " + message);
                peer?.RaiseAutomationEvent(AutomationEvents.MenuOpened);
            }
        }

        private void Apply()
        {
            Logger.LogTrace();

            LayoutModel model = _settings.SelectedModel;
            _settings.SetAppliedModel(model);
            App.Overlay.Monitors[App.Overlay.CurrentDesktop].SetLayoutSettings(model);
            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
        }

        private void OnClosing(object sender, WindowEventArgs e)
        {
            Logger.LogTrace();

            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
            App.FancyZonesEditorIO.SerializeLayoutHotkeys();
            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeDefaultLayouts();
            App.Overlay.CloseLayoutWindow();
            ((App)Application.Current).Shutdown();
        }

        private void DeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();
            EditLayoutDialog.Hide();
            DeleteLayout((FrameworkElement)sender);
        }

        private async void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            // Avoid trying to open the same dialog twice.
            if (_openedDialog != null)
            {
                return;
            }

            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is not LayoutModel model)
            {
                return;
            }

            Select(model);

            App.Overlay.StartEditing(_settings.SelectedModel);

            EditLayoutDialogTitle.Text = string.Format(CultureInfo.CurrentCulture, EditTemplate, model.Name);
            EditLayoutDialogBody.DataContext = model;
            UpdateEditLayoutDialogVisibility(model);

            EditLayoutDialog.XamlRoot = RootGrid.XamlRoot;
            await EditLayoutDialog.ShowAsync();
        }

        /// <summary>
        /// Applies the visibility rules the WPF markup expressed through the LayoutType-to-Visibility
        /// converters. WinUI's Setter cannot host a Binding, so the state is pushed here instead.
        /// </summary>
        /// <param name="model">The layout being edited.</param>
        private void UpdateEditLayoutDialogVisibility(LayoutModel model)
        {
            Visibility custom = model.IsCustom ? Visibility.Visible : Visibility.Collapsed;
            Visibility template = model.IsTemplateLayout ? Visibility.Visible : Visibility.Collapsed;

            DuplicateLayoutButton.Visibility = custom;
            DeleteLayoutButton.Visibility = custom;
            EditZoneLayoutButton.Visibility = custom;
            LayoutNamePanel.Visibility = custom;
            QuickKeyPanel.Visibility = custom;

            CreateFromTemplateLayoutButton.Visibility = template;
            ZoneCountPanel.Visibility = template;

            SpacingPanel.Visibility = model.SupportsSpacing ? Visibility.Visible : Visibility.Collapsed;

            SetLayoutAsHorizontalDefaultButton.Visibility = model.CanBeSetAsHorizontalDefault ? Visibility.Visible : Visibility.Collapsed;
            HorizontalDefaultLayoutButton.Visibility = model.IsHorizontalDefault ? Visibility.Visible : Visibility.Collapsed;
            SetLayoutAsVerticalDefaultButton.Visibility = model.CanBeSetAsVerticalDefault ? Visibility.Visible : Visibility.Collapsed;
            VerticalDefaultLayoutButton.Visibility = model.IsVerticalDefault ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EditZones_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();
            var dataContext = ((FrameworkElement)sender).DataContext;
            Select((LayoutModel)dataContext);
            EditLayoutDialog.Hide();
            AppWindow.Hide();
            App.Overlay.OpenEditor(_settings.SelectedModel);
        }

        private void MonitorScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // Turn vertical wheel input into horizontal scrolling for the monitor strip.
            var scrollViewer = sender as ScrollViewer;
            int delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;

            scrollViewer.ChangeView(scrollViewer.HorizontalOffset - delta, null, null);
            e.Handled = true;
        }

        private void NewLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Logger.LogTrace();

            LayoutModel selectedLayoutModel;

            if (GridLayoutRadioButton.IsChecked == true)
            {
                GridLayoutModel gridModel = new GridLayoutModel(LayoutNameText.Text, LayoutType.Custom)
                {
                    Rows = 1,
                    RowPercents = new List<int>(1) { GridLayoutModel.GridMultiplier },
                };
                selectedLayoutModel = gridModel;
            }
            else
            {
                var area = App.Overlay.WorkArea;
                CanvasLayoutModel canvasModel = new CanvasLayoutModel(LayoutNameText.Text, LayoutType.Custom, (int)area.Width, (int)area.Height);
                canvasModel.AddZone();
                selectedLayoutModel = canvasModel;
            }

            selectedLayoutModel.InitTemplateZones();

            AppWindow.Hide();

            App.Overlay.CurrentDataContext = selectedLayoutModel;
            App.Overlay.OpenEditor(selectedLayoutModel);
        }

        // EditLayout: Cancel changes
        private void EditLayoutDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            App.Overlay.EndEditing(_settings.SelectedModel);
            Select(_settings.AppliedModel);
        }

        // EditLayout: Save changes
        private void EditLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Logger.LogTrace();

            App.Overlay.EndEditing(null);
            LayoutModel model = _settings.SelectedModel;

            // update current settings
            if (model == _settings.AppliedModel)
            {
                App.Overlay.Monitors[App.Overlay.CurrentDesktop].SetLayoutSettings(model);
            }

            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeLayoutHotkeys();
            App.FancyZonesEditorIO.SerializeDefaultLayouts();

            // reset selected model
            Select(_settings.AppliedModel);
        }

        private async void DeleteLayout(FrameworkElement element)
        {
            Logger.LogTrace();

            var dialog = new ContentDialog()
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = ResourceLoaderInstance.GetString("Are_You_Sure"),
                Content = ResourceLoaderInstance.GetString("Are_You_Sure_Description"),
                PrimaryButtonText = ResourceLoaderInstance.GetString("Delete"),
                SecondaryButtonText = ResourceLoaderInstance.GetString("Cancel"),
            };

            Announce(ResourceLoaderInstance.GetString("Delete_Layout_Dialog_Announce"), dialog.Content.ToString());
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                LayoutModel model = element.DataContext as LayoutModel;
                MainWindowSettingsModel.DefaultLayouts.Reset(model.Uuid);

                if (model == _settings.AppliedModel)
                {
                    _settings.SetAppliedModel(_settings.BlankModel);
                    Select(_settings.BlankModel);
                }

                foreach (var monitor in App.Overlay.Monitors)
                {
                    if (monitor.Settings.ZonesetUuid == model.Uuid)
                    {
                        monitor.SetLayoutSettings(_settings.BlankModel);
                    }
                }

                model.Delete();
                App.FancyZonesEditorIO.SerializeAppliedLayouts();
                App.FancyZonesEditorIO.SerializeCustomLayouts();
                App.FancyZonesEditorIO.SerializeDefaultLayouts();
                App.FancyZonesEditorIO.SerializeLayoutHotkeys();
                App.FancyZonesEditorIO.SerializeLayoutTemplates();
            }
        }

        private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Announce(sender.Name, ResourceLoaderInstance.GetString("Edit_Layout_Open_Announce"));
            _openedDialog = sender;
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _openedDialog = null;
        }

        private void Layout_ItemClick(object sender, ItemClickEventArgs e)
        {
            Select(e.ClickedItem as LayoutModel);
            Apply();
        }

        private void Monitor_ItemClick(object sender, ItemClickEventArgs e)
        {
            _monitorViewModel.SelectCommand.Execute(e.ClickedItem as MonitorInfoModel);
        }

        /// <summary>
        /// Applies the per-container automation metadata and selection state that the WPF
        /// ItemContainerStyle set with {Binding} inside Setters - unsupported in WinUI 3.
        /// </summary>
        private void Layouts_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not LayoutModel model || args.ItemContainer is not GridViewItem container)
            {
                return;
            }

            AutomationProperties.SetName(container, model.Name ?? string.Empty);
            AutomationProperties.SetAutomationId(container, model.AutomationId ?? string.Empty);
            container.IsSelected = model.IsApplied;
        }

        private void Monitors_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not MonitorInfoModel monitor || args.ItemContainer is not GridViewItem container)
            {
                return;
            }

            AutomationProperties.SetName(container, monitor.AccessibleName ?? string.Empty);
            AutomationProperties.SetHelpText(container, monitor.AccessibleHelpText ?? string.Empty);
            container.IsSelected = monitor.Selected;
        }

        private void ComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space)
            {
                e.Handled = true;
                ComboBox selectedComboBox = sender as ComboBox;
                if (!selectedComboBox.IsDropDownOpen)
                {
                    selectedComboBox.IsDropDownOpen = true;
                }
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectionStart = tb.Text.Length;
            }
        }

        private void SensitivityInput_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            RaiseSliderNotification(SensitivityInput, "sliderValueChanged", string.Format(CultureInfo.CurrentCulture, PixelValue, SensitivityInput.Value));
        }

        private void TemplateZoneCount_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            RaiseSliderNotification(TemplateZoneCount, "templateZoneCountValueChanged", string.Format(CultureInfo.CurrentCulture, TemplateZoneCountValue, TemplateZoneCount.Value));
        }

        private void Spacing_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            RaiseSliderNotification(Spacing, "spacingValueChanged", string.Format(CultureInfo.CurrentCulture, PixelValue, Spacing.Value));
        }

        private void RaiseSliderNotification(UIElement slider, string activityId, string value)
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged) || value == null)
            {
                return;
            }

            if (FrameworkElementAutomationPeer.FromElement(slider) is SliderAutomationPeer peer)
            {
                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    value,
                    activityId);
            }
        }

        private void SetLayoutAsVerticalDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Set(model, MonitorConfigurationType.Vertical);
                UpdateEditLayoutDialogVisibility(model);
            }
        }

        private void SetLayoutAsHorizontalDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Set(model, MonitorConfigurationType.Horizontal);
                UpdateEditLayoutDialogVisibility(model);
            }
        }

        private void HorizontalDefaultLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Reset(MonitorConfigurationType.Horizontal);
                UpdateEditLayoutDialogVisibility(model);
            }
        }

        private void VerticalDefaultLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Reset(MonitorConfigurationType.Vertical);
                UpdateEditLayoutDialogVisibility(model);
            }
        }
    }
}
