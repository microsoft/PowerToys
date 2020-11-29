// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using FancyZonesEditor.ViewModels;
using Windows.UI.Popups;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO: share the constants b/w C# Editor and FancyZoneLib
        public const int MaxZones = 40;
        private const int DefaultWrapPanelItemSize = 164;
        private const int SmallWrapPanelItemSize = 164;
        private const int MinimalForDefaultWrapPanelsHeight = 900;

        private readonly MainWindowSettingsModel _settings = ((App)Application.Current).MainWindowSettings;

        // Localizable string
        private static readonly string _defaultNamePrefix = "Custom Layout ";

        public int WrapPanelItemSize { get; set; } = DefaultWrapPanelItemSize;

        public MainWindow(bool spanZonesAcrossMonitors, Rect workArea)
        {
            InitializeComponent();
            DataContext = _settings;

            KeyUp += MainWindow_KeyUp;

            if (spanZonesAcrossMonitors)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (workArea.Height < MinimalForDefaultWrapPanelsHeight || App.Overlay.MultiMonitorMode)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                WrapPanelItemSize = SmallWrapPanelItemSize;
            }
        }

        public void Update()
        {
            DataContext = _settings;
            SetSelectedItem();
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(sender, null);
            }
        }

        private void DecrementZones_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.ZoneCount > 1)
            {
                _settings.ZoneCount--;
            }
        }

        private void IncrementZones_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.ZoneCount < MaxZones)
            {
                _settings.ZoneCount++;
            }
        }

        private void NewCustomLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            WindowLayout window = new WindowLayout();
            window.Show();
            Hide();
        }

        private void LayoutItem_MouseEnter(object sender, MouseEventArgs e)
        {
            Select(((Border)sender).DataContext as LayoutModel);
        }

        private void LayoutItem_Click(object sender, MouseButtonEventArgs e)
        {
            Select(((Border)sender).DataContext as LayoutModel);
            Apply();
        }

        private void LayoutItem_Focused(object sender, RoutedEventArgs e)
        {
            Select(((Border)sender).DataContext as LayoutModel);
        }

        private void LayoutItem_Apply(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Space)
            {
                // When certain layout item (template or custom) is focused through keyboard and user
                // presses Enter or Space key, layout will be applied.
                Apply();
            }
            else if (e.Key == Key.E)
            {
                Border selectedBorder = sender as Border;

                ContentPresenter x = GetParent(selectedBorder);
                if (x != null)
                {
                    x.ContextMenu.PlacementTarget = x;
                    x.ContextMenu.IsOpen = true;
                    ((MenuItem)x.ContextMenu.Items[0]).Focus();
                }
            }
        }

        private ContentPresenter GetParent(Visual v)
        {
            while (v != null)
            {
                v = VisualTreeHelper.GetParent(v) as Visual;
                if (v is ContentPresenter)
                {
                    break;
                }
            }

            return v as ContentPresenter;
        }

        private void Select(LayoutModel newSelection)
        {
            if (App.Overlay.CurrentDataContext is LayoutModel currentSelection)
            {
                currentSelection.IsSelected = false;
            }

            newSelection.IsSelected = true;
            App.Overlay.CurrentDataContext = newSelection;
        }

        private async void NewLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            GridLayoutRadioButton.IsChecked = true;
            GridLayoutRadioButton.Focus();
            await NewLayoutDialog.ShowAsync();
        }

        private void DuplicateLayout_Click(object sender, RoutedEventArgs e)
        {
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            model.IsSelected = false;

            Hide();
            bool isPredefinedLayout = MainWindowSettingsModel.IsPredefinedLayout(model);

            if (!MainWindowSettingsModel.CustomModels.Contains(model) || isPredefinedLayout)
            {
                if (isPredefinedLayout)
                {
                    // make a copy
                    model = model.Clone();
                    mainEditor.CurrentDataContext = model;
                }

                int maxCustomIndex = 0;
                foreach (LayoutModel customModel in MainWindowSettingsModel.CustomModels)
                {
                    string name = customModel.Name;
                    if (name.StartsWith(_defaultNamePrefix))
                    {
                        if (int.TryParse(name.Substring(_defaultNamePrefix.Length), out int i))
                        {
                            if (maxCustomIndex < i)
                            {
                                maxCustomIndex = i;
                            }
                        }
                    }
                }

                model.Name = _defaultNamePrefix + (++maxCustomIndex);
            }

            mainEditor.OpenEditor(model);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        private void Apply()
        {
            ((App)Application.Current).MainWindowSettings.ResetAppliedModel();

            var mainEditor = App.Overlay;
            if (mainEditor.CurrentDataContext is LayoutModel model)
            {
                model.Apply();
            }

            if (!mainEditor.MultiMonitorMode)
            {
                Close();
            }
        }

        private void OnClosing(object sender, EventArgs e)
        {
            LayoutModel.SerializeDeletedCustomZoneSets();
            App.Overlay.CloseLayoutWindow();
            App.Current.Shutdown();
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            SetSelectedItem();
        }

        private void SetSelectedItem()
        {
            foreach (LayoutModel model in MainWindowSettingsModel.CustomModels)
            {
                if (model.IsSelected)
                {
                    // TemplateTab.SelectedItem = model;
                    return;
                }
            }
        }

        private void DeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            LayoutModel model = ((FrameworkElement)sender).DataContext as LayoutModel;
            if (model.IsSelected)
            {
                SetSelectedItem();
            }

            model.Delete();
        }

        private void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            model.IsSelected = false;
            Hide();

            mainEditor.OpenEditor(model);
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var overlay = App.Overlay;
            MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;

            if (overlay.CurrentDataContext is LayoutModel model)
            {
                model.IsSelected = false;
                model.IsApplied = false;
            }

            overlay.CurrentLayoutSettings.ZonesetUuid = settings.BlankModel.Uuid;
            overlay.CurrentLayoutSettings.Type = LayoutType.Blank;
            overlay.CurrentDataContext = settings.BlankModel;

            App.FancyZonesEditorIO.SerializeAppliedLayouts();

            if (!overlay.MultiMonitorMode)
            {
                Close();
            }
        }

        private void NewLayoutDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            LayoutModel selectedLayoutModel;

            if (GridLayoutRadioButton.IsChecked == true)
            {
                // 1:1 Copy from MainWindowSettingsModel
                int multiplier = 10000;
                int zoneCount = 3;

                GridLayoutModel columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
                {
                    Rows = 1,
                    RowPercents = new List<int>(1) { multiplier },
                };

                columnsModel.CellChildMap = new int[1, zoneCount];
                columnsModel.Columns = zoneCount;
                columnsModel.ColumnPercents = new List<int>(zoneCount);

                for (int i = 0; i < 3; i++)
                {
                    columnsModel.CellChildMap[0, i] = i;
                    columnsModel.ColumnPercents.Add(((multiplier * (i + 1)) / zoneCount) - ((multiplier * i) / zoneCount));
                }

                selectedLayoutModel = columnsModel;
            }
            else
            {
                selectedLayoutModel = new CanvasLayoutModel(Properties.Resources.Custom_Layout_Create_New, LayoutType.Blank);
            }

            App.Overlay.CurrentDataContext = selectedLayoutModel;
            var mainEditor = App.Overlay;
            Hide();
            mainEditor.OpenEditor(selectedLayoutModel);
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void OnContentRendered(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void MonitorItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Space)
            {
                monitorViewModel.SelectCommand.Execute((MonitorInfoModel)(sender as Border).DataContext);
            }
        }

        private void MonitorItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            monitorViewModel.SelectCommand.Execute((MonitorInfoModel)(sender as Border).DataContext);
        }
    }
}
