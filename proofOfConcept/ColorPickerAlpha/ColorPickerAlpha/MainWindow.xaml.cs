using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorPickerAlpha
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pnt = e.GetPosition(null);
            (int x, int y) = ColorPicker.GetPhysicalCursorCoords();
            System.Drawing.Color newCol = ColorPicker.GetPixelColor(x, y);

            txt2.Text = "Mouse Click: " + pnt.ToString();
            txt3.Text = "RGB: " + newCol.R + "," + newCol.B + "," + newCol.G;

            //Test with rectangle windows of different colors
            if (e.Source is Rectangle source)
            {
                source.Fill = source.Name.Equals("mrRec") ? Brushes.Aqua : Brushes.Red;
            }
        }
    }
}
