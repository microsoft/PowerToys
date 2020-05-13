using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
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


                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Drawing.Color color = ColorPicker.GetPixelColor(x, y);
                        
                        curColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                        Color_Box.Fill = new SolidColorBrush(curColor);

                        R_val.Text = curColor.R.ToString();
                        G_val.Text = curColor.G.ToString();
                        B_val.Text = curColor.B.ToString();

                    }));

                    Thread.Sleep(100);
                }
            }).Start();

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Copy_Clip(object sender, RoutedEventArgs e)
        {
            // RGB and ARGB formats
            StringBuilder rgb_hex = new StringBuilder();
            string argb_hex = curColor.ToString();

            // Append the # sign in hex and remove the Alpha values from the ARGB format i.e) #AARRGGBB.
            rgb_hex.Append(argb_hex[0]);
            rgb_hex.Append(argb_hex.Substring(3));

            Clipboard.SetText(rgb_hex.ToString());
        }
    }
}
