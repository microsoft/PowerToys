using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ColorPickerAlpha
{
    public partial class MainWindow : Window
    {
        Boolean rgbState = false;
        Color curColor;

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

                        HEXValue.Text = rgbToHEX(curColor.ToString());

                    }));

                    Thread.Sleep(100);
                }
            }).Start();
        }

        private void toggle_rgb(object sender, RoutedEventArgs e)
        {
            rgbState = !rgbState;
            if (rgbState)
            {
                R_val.Visibility = Visibility.Visible;
                RLabel.Visibility = Visibility.Visible;
                G_val.Visibility = Visibility.Visible;
                GLabel.Visibility = Visibility.Visible;
                B_val.Visibility = Visibility.Visible;
                BLabel.Visibility = Visibility.Visible;
                HEXValue.Visibility = Visibility.Hidden;
                HEXLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                R_val.Visibility = Visibility.Hidden;
                RLabel.Visibility = Visibility.Hidden;
                G_val.Visibility = Visibility.Hidden;
                GLabel.Visibility = Visibility.Hidden;
                B_val.Visibility = Visibility.Hidden;
                BLabel.Visibility = Visibility.Hidden;
                HEXValue.Visibility = Visibility.Visible;
                HEXLabel.Visibility = Visibility.Visible;
            }
        }

        private void Copy_Clip(object sender, RoutedEventArgs e)
        {
            
            string argb_hex = curColor.ToString();

            if (rgbState)
            {
                string rgbText = "(" + R_val.Text + ", " + G_val.Text + ", " + B_val.Text + " )";
                Clipboard.SetText(rgbText);
            }
            else
            {
                Clipboard.SetText(rgbToHEX(argb_hex));
            }
        }

        private string rgbToHEX(string rgb)
        {
            // RGB and ARGB formats
            StringBuilder rgb_hex = new StringBuilder();
            // Append the # sign in hex and remove the Alpha values from the ARGB format i.e) #AARRGGBB.
            rgb_hex.Append(rgb[0]);
            rgb_hex.Append(rgb.Substring(3));

            return rgb_hex.ToString();
        }
    }
}
