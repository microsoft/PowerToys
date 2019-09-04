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
using MahApps.Metro.Controls;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for windowEditor.xaml
    /// </summary>
    public partial class CanvasEditorWindow : EditorWindow
    {
        public CanvasEditorWindow()
        {
            InitializeComponent();
            Model = EditorOverlay.Current.DataContext as CanvasLayoutModel;
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            Model.AddZone(new Int32Rect(_offset, _offset, (int) (Model.ReferenceWidth * 0.6), (int) (Model.ReferenceHeight * 0.6)));
            _offset += 100;
        }

        private int _offset = 100;
        private CanvasLayoutModel Model;
    }
}
