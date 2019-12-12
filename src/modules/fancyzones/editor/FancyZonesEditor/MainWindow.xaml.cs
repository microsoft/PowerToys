// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        // TODO: share the constants b/w C# Editor and FancyZoneLib
        public static int MAX_ZONES = 40;
        private static string _defaultNamePrefix = "Custom Layout ";
        private bool _editing = false;
        private int _WrapPanelItemSize = 262;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _settings;
            if (_settings.WorkArea.Height < 900)
            {
                this.SizeToContent = SizeToContent.WidthAndHeight;
                this.WrapPanelItemSize = 180;
            }
        }

        public int WrapPanelItemSize
        {
            get
            {
                return _WrapPanelItemSize;
            }

            set
            {
                _WrapPanelItemSize = value;
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
            if (_settings.ZoneCount < MAX_ZONES)
            {
                _settings.ZoneCount++;
            }
        }

        private Settings _settings = ((App)Application.Current).ZoneSettings;

        private void NewCustomLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            WindowLayout window = new WindowLayout();
            window.Show();
            this.Close();
        }

        private void LayoutItem_Click(object sender, MouseButtonEventArgs e)
        {
            Select(((Border)sender).DataContext as LayoutModel);
        }

        private void Select(LayoutModel newSelection)
        {
            LayoutModel currentSelection = EditorOverlay.Current.DataContext as LayoutModel;

            if (currentSelection != null)
            {
                currentSelection.IsSelected = false;
            }

            newSelection.IsSelected = true;

            EditorOverlay.Current.DataContext = newSelection;
        }

        private void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = EditorOverlay.Current;
            LayoutModel model = mainEditor.DataContext as LayoutModel;
            if (model == null)
            {
                return;
            }

            model.IsSelected = false;
            _editing = true;
            this.Close();

            bool isPredefinedLayout = Settings.IsPredefinedLayout(model);

            if (!_settings.CustomModels.Contains(model) || isPredefinedLayout)
            {
                if (isPredefinedLayout)
                {
                    // make a copy
                    model = model.Clone();
                    mainEditor.DataContext = model;
                }

                int maxCustomIndex = 0;
                foreach (LayoutModel customModel in _settings.CustomModels)
                {
                    string name = customModel.Name;
                    if (name.StartsWith(_defaultNamePrefix))
                    {
                        int i;
                        if (Int32.TryParse(name.Substring(_defaultNamePrefix.Length), out i))
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
            LayoutModel model = mainEditor.DataContext as LayoutModel;
            if (model != null)
            {
                if (model is GridLayoutModel)
                {
                    model.Apply(mainEditor.GetZoneRects());
                }
                else
                {
                    model.Apply((model as CanvasLayoutModel).Zones.ToArray());
                }

                this.Close();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (!_editing)
            {
                EditorOverlay.Current.Close();
            }
        }

        private void InitializedEventHandler(object sender, EventArgs e)
        {
            SetSelectedItem();
        }

        private void SetSelectedItem()
        {
            foreach (LayoutModel model in _settings.CustomModels)
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


    public class BooleanToBrushConverter : IValueConverter
    {
        private static Brush c_selectedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        private static Brush c_normalBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? c_selectedBrush : c_normalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value == c_selectedBrush;
        }
    }

    public class ModelToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Settings.IsPredefinedLayout((LayoutModel)value) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class BooleanToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                return (bool)value == true ? 1 : 0;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                return (int)value == 1;
            }

            return false;
        }
    }
}
