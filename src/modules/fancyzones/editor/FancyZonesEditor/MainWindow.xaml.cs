// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

using Common.UI;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using ManagedCommon;
using ModernWpf.Controls;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int DefaultWrapPanelItemSize = 164;
        private const int SmallWrapPanelItemSize = 164;
        private const int MinimalForDefaultWrapPanelsHeight = 900;

        private readonly MainWindowSettingsModel _settings = ((App)Application.Current).MainWindowSettings;

        private ContentDialog _openedDialog;
        private TextBlock _createLayoutAnnounce;

        private bool haveTriedToGetFocusAlready;

        private static readonly CompositeFormat EditTemplate = System.Text.CompositeFormat.Parse(Properties.Resources.Edit_Template);
        private static readonly CompositeFormat PixelValue = System.Text.CompositeFormat.Parse(Properties.Resources.Pixel_Value);
        private static readonly CompositeFormat TemplateZoneCountValue = System.Text.CompositeFormat.Parse(Properties.Resources.Template_Zone_Count_Value);

        public int WrapPanelItemSize { get; set; } = DefaultWrapPanelItemSize;

        public MainWindow(bool spanZonesAcrossMonitors, Rect workArea)
        {
            InitializeComponent();
            _createLayoutAnnounce = (TextBlock)FindName("LayoutCreationAnnounce");
            DataContext = _settings;

            KeyUp += MainWindow_KeyUp;
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            if (spanZonesAcrossMonitors)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (workArea.Height < MinimalForDefaultWrapPanelsHeight || App.Overlay.MultiMonitorMode)
            {
                WrapPanelItemSize = SmallWrapPanelItemSize;
            }

            MaxWidth = workArea.Width;
            MaxHeight = workArea.Height;
            SizeToContent = SizeToContent.WidthAndHeight;

            // reinit considering work area rect
            _settings.InitModels();
        }

        private void BringToFront()
        {
            // Get the window handle of the FancyZones Editor window
            IntPtr handle = new WindowInteropHelper(this).Handle;

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

            haveTriedToGetFocusAlready = true;
        }

        public void Update()
        {
            DataContext = _settings;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseDialog(sender);
            }
        }

        // Prevent closing the dialog with enter
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _openedDialog != null && _openedDialog.IsVisible)
            {
                if (e.OriginalSource is RadioButton source && source.IsChecked != true)
                {
                    source.IsChecked = true;
                    e.Handled = true;
                }
            }
        }

        private void LayoutItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CloseDialog(sender);
        }

        private void CloseDialog(object sender)
        {
            if (_openedDialog != null)
            {
                _openedDialog.Hide();
            }
            else
            {
                OnClosing(sender, null);
            }
        }

        private void DecrementZones_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedModel.TemplateZoneCount > 1)
            {
                _settings.SelectedModel.TemplateZoneCount--;
            }
        }

        private void IncrementZones_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedModel.IsZoneAddingAllowed)
            {
                _settings.SelectedModel.TemplateZoneCount++;
            }
        }

        private void LayoutItem_MouseEnter(object sender, MouseEventArgs e)
        {
            // Select(((Grid)sender).DataContext as LayoutModel);
        }

        private void LayoutItem_Focused(object sender, RoutedEventArgs e)
        {
            // Ignore focus on Edit button click
            if (e.Source is Button)
            {
                return;
            }

            Select(((Border)sender).DataContext as LayoutModel);
        }

        private void LayoutItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Space)
            {
                // When certain layout item (template or custom) is focused through keyboard and user
                // presses Enter or Space key, layout will be applied.
                Apply();
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

            string defaultNamePrefix = FancyZonesEditor.Properties.Resources.Default_Custom_Layout_Name;
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
            GridLayoutRadioButton.Focus();
            await NewLayoutDialog.ShowAsync();
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

            Announce(name, FancyZonesEditor.Properties.Resources.Layout_Creation_Announce);
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
            if (AutomationPeer.ListenerExists(AutomationEvents.MenuOpened) && _createLayoutAnnounce != null)
            {
                var peer = UIElementAutomationPeer.FromElement(_createLayoutAnnounce);
                AutomationProperties.SetName(_createLayoutAnnounce, name + " " + message);
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

        private void OnClosing(object sender, EventArgs e)
        {
            Logger.LogTrace();

            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
            App.FancyZonesEditorIO.SerializeLayoutHotkeys();
            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeDefaultLayouts();
            App.Overlay.CloseLayoutWindow();
            App.Current.Shutdown();
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
            Select((LayoutModel)dataContext);

            App.Overlay.StartEditing(_settings.SelectedModel);

            Keyboard.ClearFocus();
            EditLayoutDialogTitle.Text = string.Format(CultureInfo.CurrentCulture, EditTemplate, ((LayoutModel)dataContext).Name);
            await EditLayoutDialog.ShowAsync();
        }

        private void EditZones_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();
            var dataContext = ((FrameworkElement)sender).DataContext;
            Select((LayoutModel)dataContext);
            EditLayoutDialog.Hide();
            Hide();
            App.Overlay.OpenEditor(_settings.SelectedModel);
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scrollviewer = sender as ScrollViewer;
            if (e.Delta > 0)
            {
                scrollviewer.LineLeft();
            }
            else
            {
                scrollviewer.LineRight();
            }

            e.Handled = true;
        }

        private void NewLayoutDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
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

            try
            {
                Hide();
            }
            catch
            {
                // See https://github.com/microsoft/PowerToys/issues/9396
                Hide();
            }

            App.Overlay.CurrentDataContext = selectedLayoutModel;
            App.Overlay.OpenEditor(selectedLayoutModel);
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void OnContentRendered(object sender, EventArgs e)
        {
            if (!haveTriedToGetFocusAlready)
            {
                BringToFront();
            }

            InvalidateVisual();
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
                Title = Properties.Resources.Are_You_Sure,
                Content = Properties.Resources.Are_You_Sure_Description,
                PrimaryButtonText = Properties.Resources.Delete,
                SecondaryButtonText = Properties.Resources.Cancel,
            };

            Announce(FancyZonesEditor.Properties.Resources.Delete_Layout_Dialog_Announce, dialog.Content.ToString());
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
            Announce(sender.Name, FancyZonesEditor.Properties.Resources.Edit_Layout_Open_Announce);
            _openedDialog = sender;
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _openedDialog = null;
        }

        private void EditDialogNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Making sure that pressing Enter when changing values in a NumberBox will not close the edit dialog.
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }

        private void Layout_ItemClick(object sender, ItemClickEventArgs e)
        {
            Select(e.ClickedItem as LayoutModel);
            Apply();
        }

        private void Monitor_ItemClick(object sender, ItemClickEventArgs e)
        {
            monitorViewModel.SelectCommand.Execute(e.ClickedItem as MonitorInfoModel);
        }

        private void ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                e.Handled = true;
                ComboBox selectedComboBox = sender as ComboBox;
                if (!selectedComboBox.IsDropDownOpen)
                {
                    selectedComboBox.IsDropDownOpen = true;
                }
            }
        }

        private void TextBox_GotKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectionStart = tb.Text.Length;
            }
        }

        private void NumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            // The TextBox inside a NumberBox doesn't inherit the Automation Properties name, so we have to set it.
            var numberBox = sender as NumberBox;
            const string numberBoxTextBoxName = "InputBox"; // Text box template part name given by ModernWPF.
            numberBox.ApplyTemplate(); // Apply template to be able to change child's property.
            var numberBoxTextBox = numberBox.Template.FindName(numberBoxTextBoxName, numberBox) as TextBox;
            numberBoxTextBox.SetValue(AutomationProperties.NameProperty, numberBox.GetValue(AutomationProperties.NameProperty));
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.FancyZones, false);
        }

        private void EditLayoutDialogTitle_Loaded(object sender, RoutedEventArgs e)
        {
            EditLayoutDialogTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            EditLayoutDialogTitle.TextWrapping = TextWrapping.NoWrap;
        }

        private void SensitivityInput_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                SliderAutomationPeer peer =
                    FrameworkElementAutomationPeer.FromElement(SensitivityInput) as SliderAutomationPeer;
                string activityId = "sliderValueChanged";

                string value = string.Format(CultureInfo.CurrentCulture, PixelValue, SensitivityInput.Value);

                if (peer != null && value != null)
                {
                    peer.RaiseNotificationEvent(
                        AutomationNotificationKind.ActionCompleted,
                        AutomationNotificationProcessing.ImportantMostRecent,
                        value,
                        activityId);
                }
            }
        }

        private void TemplateZoneCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                SliderAutomationPeer peer =
                    FrameworkElementAutomationPeer.FromElement(TemplateZoneCount) as SliderAutomationPeer;
                string activityId = "templateZoneCountValueChanged";

                string value = string.Format(CultureInfo.CurrentCulture, TemplateZoneCountValue, TemplateZoneCount.Value);

                if (peer != null && value != null)
                {
                    peer.RaiseNotificationEvent(
                        AutomationNotificationKind.ActionCompleted,
                        AutomationNotificationProcessing.ImportantMostRecent,
                        value,
                        activityId);
                }
            }
        }

        private void Spacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                SliderAutomationPeer peer =
                    FrameworkElementAutomationPeer.FromElement(Spacing) as SliderAutomationPeer;
                string activityId = "spacingValueChanged";

                string value = string.Format(CultureInfo.CurrentCulture, PixelValue, Spacing.Value);

                if (peer != null && value != null)
                {
                    peer.RaiseNotificationEvent(
                        AutomationNotificationKind.ActionCompleted,
                        AutomationNotificationProcessing.ImportantMostRecent,
                        value,
                        activityId);
                }
            }
        }

        private void SetLayoutAsVerticalDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Set(model, MonitorConfigurationType.Vertical);
            }
        }

        private void SetLayoutAsHorizontalDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Set(model, MonitorConfigurationType.Horizontal);
            }
        }

        private void HorizontalDefaultLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Reset(MonitorConfigurationType.Horizontal);
            }
        }

        private void VerticalDefaultLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            if (dataContext is LayoutModel model)
            {
                MainWindowSettingsModel.DefaultLayouts.Reset(MonitorConfigurationType.Vertical);
            }
        }
    }
}
