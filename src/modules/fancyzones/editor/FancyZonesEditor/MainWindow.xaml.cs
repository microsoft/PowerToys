// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;
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
        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;
        private static readonly string _defaultNamePrefix = "Custom Layout ";

        public int WrapPanelItemSize { get; set; } = 262;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _settings;

            KeyUp += MainWindow_KeyUp;

            if (_settings.WorkArea.Height < 900)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                WrapPanelItemSize = 180;
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

            window.Owner = EditorOverlay.Current;
            window.DataContext = model;
            window.Show();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = EditorOverlay.Current;
            if (mainEditor.DataContext is LayoutModel model)
            {
                if (model is GridLayoutModel)
                {
                    model.Apply(mainEditor.GetZoneRects());
                }
                else
                {
                    model.Apply((model as CanvasLayoutModel).Zones.ToArray());
                }

                Close();
            }
        }

        private void OnClosing(object sender, EventArgs e)
        {
            LayoutModel.SerializeDeletedCustomZoneSets();
            EditorOverlay.Current.Close();
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
