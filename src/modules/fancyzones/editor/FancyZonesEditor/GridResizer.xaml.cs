using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for GridResizer.xaml
    /// </summary>
    public partial class GridResizer : Thumb
    {
        public GridResizer()
        {
            InitializeComponent();
        }

        public Orientation Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                _orientation = value;
                ApplyTemplate();
                StackPanel body = (StackPanel)Template.FindName("Body", this);
                if (value == Orientation.Vertical)
                {
                    body.RenderTransform = null;
                    body.Cursor = Cursors.SizeWE;
                }
                else
                {
                    body.RenderTransform = c_rotateTransform;
                    body.Cursor = Cursors.SizeNS;
                }
            }
        }

        private static RotateTransform c_rotateTransform = new RotateTransform(90, 24, 24);
    
        public int Index;
        public LayoutModel Model;

        private Orientation _orientation;
        
    }
}
