using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorPickerAlpha
{
    public partial class MainWindow : Window
    {
        System.Windows.Media.Color curColor;

        public MainWindow()
        {
            InitializeComponent();

            new Thread(() =>
            {
                while (true)
                {
                    (int x, int y) = ColorPicker.GetPhysicalCursorCoords();
                    Debug.WriteLine(x);

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Drawing.Color color = ColorPicker.GetPixelColor(x, y); //GetPixel(new System.Drawing.Point(x, y));
                        Debug.WriteLine(color);
                        //txt2.Text = color.R + ",";
                        curColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                        mrRec.Fill = new SolidColorBrush(curColor);
                    }));

                    Thread.Sleep(100);
                }
            }).Start();

        }
    }
}
