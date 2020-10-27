// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using FancyZonesEditor.ViewModels;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        // TODO: share the constants b/w C# Editor and FancyZoneLib
        public const int MaxZones = 40;
        private const int DefaultWrapPanelItemSize = 262;
        private const int SmallWrapPanelItemSize = 180;
        private const int MinimalForDefaultWrapPanelsHeight = 900;

        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;

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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _settings;

            KeyUp += MainWindow_KeyUp;

            if (Settings.SpanZonesAcrossMonitors)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (WorkArea.WorkingAreaRect.Height < MinimalForDefaultWrapPanelsHeight || MonitorViewModel.IsDesktopsPanelVisible)
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
            if (EditorOverlay.Current.DataContext is LayoutModel currentSelection)
            {
                currentSelection.IsSelected = false;
            }

            newSelection.IsSelected = true;
            EditorOverlay.Current.DataContext = newSelection;
        }

        private void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = EditorOverlay.Current;
            if (!(mainEditor.DataContext is LayoutModel model))
            {
                return;
            }

            model.IsSelected = false;
            Hide();

            bool isPredefinedLayout = Settings.IsPredefinedLayout(model);

            if (!Settings.CustomModels.Contains(model) || isPredefinedLayout)
            {
                if (isPredefinedLayout)
                {
                    // make a copy
                    model = model.Clone();
                    mainEditor.DataContext = model;
                }

                int maxCustomIndex = 0;
                foreach (LayoutModel customModel in Settings.CustomModels)
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

            mainEditor.OpenEditor();

            EditorWindow window;
            bool isGrid = false;
            if (model is GridLayoutModel)
            {
                window = new GridEditorWindow();
                isGrid = true;
            }
            else
            {
                window = new CanvasEditorWindow();
            }

            window.Owner = EditorOverlay.Current.LayoutWindow;
            window.DataContext = model;
            window.Show();

            if (isGrid)
            {
                (window as GridEditorWindow).NameTextBox().Focus();
            }

            window.LeftWindowCommands = null;
            window.RightWindowCommands = null;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        private void Apply()
        {
            EditorOverlay mainEditor = EditorOverlay.Current;

            if (mainEditor.DataContext is LayoutModel model)
            {
                // If custom canvas layout has been scaled, persisting is needed
                if (model is CanvasLayoutModel && (model as CanvasLayoutModel).IsScaled)
                {
                    model.Persist();
                }
                else
                {
                    model.Apply();
                }
            }
        }

        private void OnClosing(object sender, EventArgs e)
        {
            LayoutModel.SerializeDeletedCustomZoneSets();
            EditorOverlay.Current.CloseLayoutWindow();
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            SetSelectedItem();
        }

        private void SetSelectedItem()
        {
            foreach (LayoutModel model in Settings.CustomModels)
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
    }
}
