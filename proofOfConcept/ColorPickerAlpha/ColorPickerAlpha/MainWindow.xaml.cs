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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorPickerAlpha
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Boolean rgbState = false;

        public MainWindow()
        {
            InitializeComponent();
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
