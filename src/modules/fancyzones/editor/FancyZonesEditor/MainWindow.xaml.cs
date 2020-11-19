// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO: share the constants b/w C# Editor and FancyZoneLib
        public const int MaxZones = 40;
        private const int DefaultWrapPanelItemSize = 262;
        private const int SmallWrapPanelItemSize = 180;
        private const int MinimalForDefaultWrapPanelsHeight = 900;

        private readonly MainWindowSettingsModel _settings = ((App)Application.Current).MainWindowSettings;

        // Localizable string
        private static readonly string _defaultNamePrefix = "Custom Layout ";

        public int WrapPanelItemSize { get; set; } = DefaultWrapPanelItemSize;

        public double SettingsTextMaxWidth
        {
            get
            {
                return (Width / 2) - 60;
            }
        }

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

        private void LayoutItem_Click(object sender, MouseButtonEventArgs e)
        {
            Select(((Border)sender).DataContext as LayoutModel);
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

        private void EditLayout_Click(object sender, RoutedEventArgs e)
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
                    TemplateTab.SelectedItem = model;
                    return;
                }
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            LayoutModel model = ((FrameworkElement)sender).DataContext as LayoutModel;
            if (model.IsSelected)
            {
                SetSelectedItem();
            }

            model.Delete();
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
    }
}
