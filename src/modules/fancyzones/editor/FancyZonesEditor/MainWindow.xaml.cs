using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FancyZonesEditor.Models;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
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

        private int _WrapPanelItemSize = 262;
        public int WrapPanelItemSize {
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
            if (_settings.ZoneCount < 40)
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

        private static string c_defaultNamePrefix = "Custom Layout ";
        private bool _editing = false;

        private void EditLayout_Click(object sender, RoutedEventArgs e)
        {
            _editing = true;
            this.Close();

            EditorOverlay mainEditor = EditorOverlay.Current;
            LayoutModel model = mainEditor.DataContext as LayoutModel;
            if (model == null)
            {
                mainEditor.Close();
                return;
            }
            model.IsSelected = false;

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
                    if (name.StartsWith(c_defaultNamePrefix))
                    {
                        int i;
                        if (Int32.TryParse(name.Substring(c_defaultNamePrefix.Length), out i))
                        {
                            if (maxCustomIndex < i)
                            {
                                maxCustomIndex = i;
                            }
                        }
                    }
                }
                model.Name = c_defaultNamePrefix + (++maxCustomIndex);
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
            }

            this.Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (!_editing)
            {
                EditorOverlay.Current.Close();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            foreach(LayoutModel model in _settings.CustomModels)
            {
                if (model.IsSelected)
                {
                    TemplateTab.SelectedIndex = 1;
                    return;
                }
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            LayoutModel model = ((FrameworkElement)sender).DataContext as LayoutModel;
            if (model.IsSelected)
            {
                OnLoaded(null, null);
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
}
