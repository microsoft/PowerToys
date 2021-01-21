// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
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
        private LayoutModel _backup = null;

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
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            if (model.TemplateZoneCount > 1)
            {
                model.TemplateZoneCount--;
            }
        }

        private void IncrementZones_Click(object sender, RoutedEventArgs e)
        {
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            if (model.TemplateZoneCount < LayoutSettings.MaxZones)
            {
                model.TemplateZoneCount++;
            }
        }

        private void LayoutItem_MouseEnter(object sender, MouseEventArgs e)
        {
            // Select(((Grid)sender).DataContext as LayoutModel);
        }

        private void LayoutItem_Click(object sender, MouseButtonEventArgs e)
        {
            LayoutModel selectedLayoutModel = ((Grid)sender).DataContext as LayoutModel;
            Select(selectedLayoutModel);
            Apply();
        }

        private void LayoutItem_Focused(object sender, RoutedEventArgs e)
        {
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
            LayoutNameText.Text = string.Empty;
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

            // make a copy
            model = model.Clone();
            mainEditor.CurrentDataContext = model;

            string name = model.Name;
            var index = name.LastIndexOf('(');
            if (index != -1)
            {
                name = name.Remove(index);
                name = name.TrimEnd();
            }

            int maxCustomIndex = 0;
            foreach (LayoutModel customModel in MainWindowSettingsModel.CustomModels)
            {
                string customModelName = customModel.Name;
                if (customModelName.StartsWith(name))
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

            App.Overlay.SaveCurrentLayoutSettings(model);
            App.FancyZonesEditorIO.SerializeZoneSettings();
        }

        private void Apply()
        {
            var mainEditor = App.Overlay;
            if (mainEditor.CurrentDataContext is LayoutModel model)
            {
                _settings.SetAppliedModel(model);
                App.Overlay.SaveCurrentLayoutSettings(model);
                App.FancyZonesEditorIO.SerializeZoneSettings();
            }
        }

        private void OnClosing(object sender, EventArgs e)
        {
            App.FancyZonesEditorIO.SerializeZoneSettings();
            App.Overlay.CloseLayoutWindow();
            App.Current.Shutdown();
        }

        private async void DeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ModernWpf.Controls.ContentDialog()
            {
                Title = FancyZonesEditor.Properties.Resources.Are_You_Sure,
                Content = FancyZonesEditor.Properties.Resources.Are_You_Sure_Description,
                PrimaryButtonText = FancyZonesEditor.Properties.Resources.Delete,
                SecondaryButtonText = FancyZonesEditor.Properties.Resources.Cancel,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LayoutModel model = ((FrameworkElement)sender).DataContext as LayoutModel;
                model.Delete();
            }
        }

        private async void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            var dataContext = ((FrameworkElement)sender).DataContext;
            _settings.SetSelectedModel((LayoutModel)dataContext);

            if (_settings.SelectedModel is GridLayoutModel grid)
            {
                _backup = new GridLayoutModel(grid);
            }
            else if (_settings.SelectedModel is CanvasLayoutModel canvas)
            {
                _backup = new CanvasLayoutModel(canvas);
            }

            await EditLayoutDialog.ShowAsync();
        }

        private void EditZones_Click(object sender, RoutedEventArgs e)
        {
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            _settings.SetSelectedModel(model);

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

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var overlay = App.Overlay;

            if (overlay.CurrentDataContext is LayoutModel model)
            {
                model.IsSelected = false;
                model.IsApplied = false;
            }

            overlay.CurrentLayoutSettings.ZonesetUuid = MainWindowSettingsModel.BlankModel.Uuid;
            overlay.CurrentLayoutSettings.Type = LayoutType.Blank;
            overlay.CurrentDataContext = MainWindowSettingsModel.BlankModel;

            App.FancyZonesEditorIO.SerializeZoneSettings();
        }

        private void NewLayoutDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            LayoutModel selectedLayoutModel;

            if (GridLayoutRadioButton.IsChecked == true)
            {
                // 1:1 Copy from MainWindowSettingsModel, so probably needs to be refactored / combined.
                int multiplier = 10000;
                int zoneCount = 3;

                GridLayoutModel columnsModel = new GridLayoutModel(LayoutNameText.Text, LayoutType.Columns)
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
                selectedLayoutModel = new CanvasLayoutModel(LayoutNameText.Text, LayoutType.Blank);
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

        // EditLayout: Cancel changes
        private void EditLayoutDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // restore model properties from settings
            _settings.RestoreSelectedModel(_backup);
            _backup = null;

            Select(_settings.AppliedModel);
        }

        // EditLayout: Save changes
        private void EditLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var mainEditor = App.Overlay;
            if (!(mainEditor.CurrentDataContext is LayoutModel model))
            {
                return;
            }

            _backup = null;

            // update current settings
            if (model == _settings.AppliedModel)
            {
                App.Overlay.SaveCurrentLayoutSettings(model);
            }

            App.FancyZonesEditorIO.SerializeZoneSettings();

            // reset selected model
            Select(_settings.AppliedModel);
        }
    }
}
