using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FancyZonesEditor.Models;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
    public class EditorWindow : MetroWindow
    {
        protected void OnSaveApplyTemplate(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = EditorOverlay.Current;
            LayoutModel model = mainEditor.DataContext as LayoutModel;
            if (model != null)
            {
                model.Persist(mainEditor.GetZoneRects());
            }
            _choosing = true;
            this.Close();
            EditorOverlay.Current.Close();
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            if (!_choosing)
            {
                EditorOverlay.Current.ShowLayoutPicker();
            }
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            _choosing = true;
            this.Close();
            EditorOverlay.Current.ShowLayoutPicker();
        }

        private bool _choosing = false;
    }
}
