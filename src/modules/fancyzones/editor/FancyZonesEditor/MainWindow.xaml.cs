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
    public sealed partial class MainWindow : OverlayChildWindow
    {
        private const int MinimalForDefaultWrapPanelsHeight = 900;
        private const int DefaultWrapPanelItemSize = 164;
        private const int SmallWrapPanelItemSize = 164;

        private static CompositeFormat _editTemplate;
        private static CompositeFormat _pixelValue;
        private static CompositeFormat _templateZoneCountValue;

        private readonly MainWindowSettingsModel _settings = ((App)Application.Current).MainWindowSettings;
        private readonly MonitorViewModel _monitorViewModel = new MonitorViewModel();

        private ContentDialog _openedDialog;
        private LayoutModel _editingDialogModel;
        private bool _haveTriedToGetFocusAlready;
        private bool _isLoaded;

        public MainWindow(bool spanZonesAcrossMonitors, Rect workArea)
        {
            InitializeComponent();

            // Spanning zones across monitors gives us a single overlay covering the whole virtual
            // desktop, so the picker is centered on the primary display instead - the WPF markup
            // switched to WindowStartupLocation="CenterScreen" for the same reason.
            CenterOnPrimaryDisplay = spanZonesAcrossMonitors;

            PrePlaceOnOverlayMonitor();

            Title = ResourceLoaderInstance.GetString("Fancy_Zones_Editor_App_Title");
            RootGrid.DataContext = _settings;

            Monitors.ItemsSource = _monitorViewModel.MonitorInfoForViewModel;
            TemplatesGridView.ItemsSource = MainWindowSettingsModel.TemplateModels;
            CustomGridView.ItemsSource = MainWindowSettingsModel.CustomModels;

            MainWindowSettingsModel.CustomModels.CollectionChanged += CustomModels_CollectionChanged;
            _settings.PropertyChanged += Settings_PropertyChanged;
            UpdateEmptyCustomLayoutsMessage();
            SyncAppliedSelection();
            Monitors.SelectedIndex = App.Overlay.CurrentDesktop;

            // WinUI cannot x:Bind to a named element from a Window-rooted XAML tree, so the
            // accessibility labels the WPF markup declared are wired up here instead.
            AutomationProperties.SetLabeledBy(TemplatesGridView, TemplatesHeaderBlock);
            AutomationProperties.SetLabeledBy(CustomGridView, CustomHeaderBlock);
            AutomationProperties.SetLabeledBy(QuickKeySelectionComboBox, QuickKeyTitle);
            AutomationProperties.SetLabeledBy(LayoutNameText, NameHeaderText);

            RootGrid.KeyUp += MainWindow_KeyUp;
            RootGrid.Loaded += RootGrid_Loaded;

            // The ScrollViewer handles the wheel itself, so the horizontal-scroll shim has to be
            // registered for already-handled events; WPF used the tunneling PreviewMouseWheel.
            MonitorScrollViewer.AddHandler(
                UIElement.PointerWheelChangedEvent,
                new PointerEventHandler(MonitorScrollViewer_PointerWheelChanged),
                handledEventsToo: true);

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
            NativeMethods.SetWindowOwner(Hwnd, ownerHwnd);
        }

        /// <summary>
        /// Brings the picker back after a zone editing session concealed it.
        /// </summary>
        public void Reveal()
        {
            RevealWindow();
        }

        public void Update()
        {
            RootGrid.DataContext = null;
            RootGrid.DataContext = _settings;
            SyncAppliedSelection();
            Monitors.SelectedIndex = App.Overlay.CurrentDesktop;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowSettingsModel.AppliedModel))
            {
                SyncAppliedSelection();
            }
        }

        /// <summary>
        /// Keeps the highlighted card on the layout that is actually applied. The WPF
        /// ItemContainerStyle bound <c>IsSelected</c> to <c>IsApplied</c> and therefore tracked
        /// the model live; WinUI cannot bind inside a Setter, so the GridView's own selection is
        /// driven from the applied model instead.
        /// </summary>
        private void SyncAppliedSelection()
        {
            LayoutModel applied = _settings.AppliedModel;

            TemplatesGridView.SelectedItem = MainWindowSettingsModel.TemplateModels.Contains(applied) ? applied : null;
            CustomGridView.SelectedItem = MainWindowSettingsModel.CustomModels.Contains(applied) ? applied : null;
        }

        private static bool IsCtrlKeyDown()
        {
            return Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            SizeToContentAndCenter();

            if (!_haveTriedToGetFocusAlready)
            {
                BringWindowToFront();
            }
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

            // WPF's SizeToContent stayed active for the window's lifetime; here it is a one-shot
            // measurement, so adding or removing a custom layout has to re-run it.
            if (_isLoaded)
            {
                SizeToContentAndCenter();
            }
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

        // Prevent Enter from committing the dialog while the focus is on a layout-type radio
        // button that is not checked yet. Handled on the dialog itself: it is hosted in a popup,
        // so a handler on the window's root grid never sees these keys.
        private void Dialog_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && ReferenceEquals(sender, EditLayoutDialog))
            {
                if (FinishDialogEditing(restore: true))
                {
                    Select(_settings.AppliedModel);
                }

                EditLayoutDialog.Hide();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter && e.OriginalSource is RadioButton source && source.IsChecked != true)
            {
                source.IsChecked = true;
                e.Handled = true;
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
                if (ReferenceEquals(_openedDialog, EditLayoutDialog) && FinishDialogEditing(restore: true))
                {
                    Select(_settings.AppliedModel);
                }

                _openedDialog.Hide();
            }
            else
            {
                // Let the regular close path run: Closed -> OnClosing -> App.Shutdown.
                Close();
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
            if (dataContext is not LayoutModel model)
            {
                return;
            }

            // Clone the values currently shown in the dialog, then restore the source layout.
            // "Duplicate" creates a new layout; it must not implicitly commit unsaved source edits.
            model = model.Clone();

            if (FinishDialogEditing(restore: true))
            {
                Select(_settings.AppliedModel);
            }

            if (ReferenceEquals(_openedDialog, EditLayoutDialog))
            {
                EditLayoutDialog.Hide();
            }

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

            var app = (App)Application.Current;
            if (app.IsShuttingDown)
            {
                return;
            }

            // Let this picker finish closing, but do not tear down the remaining overlay windows
            // while WinUI is still unwinding the native close callback. Re-entering
            // Application.Exit here can leave an input or WindowMessageMonitor callback using
            // XAML objects that have already been destroyed.
            if (!DispatcherQueue.TryEnqueue(app.Shutdown))
            {
                // The queue is already unavailable. Preserve settings synchronously, then let the
                // OS reclaim the UI rather than re-entering WinUI teardown on this callback stack.
                app.PersistSettings();
                Environment.Exit(0);
            }
        }

        private bool FinishDialogEditing(bool restore)
        {
            LayoutModel model = _editingDialogModel;
            if (model == null)
            {
                return false;
            }

            _editingDialogModel = null;
            App.Overlay.EndEditing(restore ? model : null);
            return true;
        }

        private void DeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            if (FinishDialogEditing(restore: true))
            {
                Select(_settings.AppliedModel);
            }

            if (ReferenceEquals(_openedDialog, EditLayoutDialog))
            {
                EditLayoutDialog.Hide();
            }

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
            _editingDialogModel = model;

            EditLayoutDialogTitle.Text = string.Format(CultureInfo.CurrentCulture, EditTemplate, model.Name);
            EditLayoutDialog.DataContext = model;
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
            if (dataContext is not LayoutModel model)
            {
                return;
            }

            Select(model);

            // Transfer the properties-dialog backup into the zone editor. Direct context-menu
            // entry has no dialog backup, so Overlay.OpenEditor creates one for that path.
            if (ReferenceEquals(_editingDialogModel, model))
            {
                _editingDialogModel = null;
            }

            if (ReferenceEquals(_openedDialog, EditLayoutDialog))
            {
                EditLayoutDialog.Hide();
            }

            ConcealWindow();
            App.Overlay.OpenEditor(model);
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

            ConcealWindow();

            App.Overlay.CurrentDataContext = selectedLayoutModel;
            App.Overlay.OpenEditor(selectedLayoutModel);
        }

        // EditLayout: Cancel changes
        private void EditLayoutDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            FinishDialogEditing(restore: true);
            Select(_settings.AppliedModel);
        }

        // EditLayout: Save changes
        private void EditLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Logger.LogTrace();

            FinishDialogEditing(restore: false);
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
            if (ReferenceEquals(sender, EditLayoutDialog) && FinishDialogEditing(restore: true))
            {
                Select(_settings.AppliedModel);
            }

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

        private void Monitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                _monitorViewModel.SelectCommand.Execute(e.AddedItems[0] as MonitorInfoModel);
            }
        }

        /// <summary>
        /// Applies the per-container automation metadata that the WPF ItemContainerStyle set with
        /// {Binding} inside Setters - unsupported in WinUI 3. The applied-layout highlight is not
        /// set here: it is driven by <see cref="SyncAppliedSelection"/> so it keeps tracking the
        /// model after the container has been realized.
        /// </summary>
        private void Layouts_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not LayoutModel model || args.ItemContainer is not GridViewItem container)
            {
                return;
            }

            AutomationProperties.SetName(container, model.Name ?? string.Empty);
            AutomationProperties.SetAutomationId(container, model.AutomationId ?? string.Empty);
        }

        private void Monitors_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not MonitorInfoModel monitor || args.ItemContainer is not GridViewItem container)
            {
                return;
            }

            AutomationProperties.SetName(container, monitor.AccessibleName ?? string.Empty);
            AutomationProperties.SetHelpText(container, monitor.AccessibleHelpText ?? string.Empty);
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
