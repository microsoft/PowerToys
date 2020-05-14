// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Security.RightsManagement;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FancyZonesEditor.Models;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public const int MaxZones = 40;
        public Settings Settings = App.ZoneSettings[MonitorVM.CurrentMonitor];
        private static readonly string _defaultNamePrefix = "Custom Layout ";

        public int WrapPanelItemSize { get; set; } = 150;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = Settings;

            KeyUp += MainWindow_KeyUp;

            if (Settings.WorkArea.Height < 900)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                WrapPanelItemSize = 150;
            }
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
            if (Settings.ZoneCount > 1)
            {
                Settings.ZoneCount--;
            }
        }

        private void IncrementZones_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.ZoneCount < MaxZones)
            {
                Settings.ZoneCount++;
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

        private void Select(LayoutModel newSelection)
        {
            if (App.Overlay[MonitorVM.CurrentMonitor].DataContext is LayoutModel currentSelection)
            {
                currentSelection.IsSelected = false;
            }

            newSelection.IsSelected = true;
            App.Overlay[MonitorVM.CurrentMonitor].DataContext = newSelection;
        }

        private void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = App.Overlay[MonitorVM.CurrentMonitor];
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

            mainEditor.Edit();

            EditorWindow window;
            if (model is GridLayoutModel)
            {
                window = new GridEditorWindow();
            }
            else
            {
                window = new CanvasEditorWindow();
            }

            window.Owner = App.Overlay[MonitorVM.CurrentMonitor];
            window.DataContext = model;
            window.Show();
        }

        public void Apply_Click(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = App.Overlay[MonitorVM.CurrentMonitor];
            if (mainEditor.DataContext is LayoutModel model)
            {
                if (model is GridLayoutModel)
                {
                    model.Apply();
                }
                else
                {
                    model.Apply();
                }

                Close();
            }
        }

        private void OnClosing(object sender, EventArgs e)
        {
            LayoutModel.SerializeDeletedCustomZoneSets();
            App.Overlay[MonitorVM.CurrentMonitor].Close();
            App.ActiveMonitors[MonitorVM.CurrentMonitor] = false;
            for (int i = MonitorVM.CurrentMonitor - 1; i >= 0; i--)
            {
                if (App.ActiveMonitors[i])
                {
                    MonitorVM.CurrentMonitor = i;
                }
            }
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
    }
}