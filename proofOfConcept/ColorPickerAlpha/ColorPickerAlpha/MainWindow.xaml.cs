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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            rgbState = !rgbState;
            if (rgbState)
            {
                Rvalue.Visibility = Visibility.Visible;
                RLabel.Visibility = Visibility.Visible;
                Gvalue.Visibility = Visibility.Visible;
                GLabel.Visibility = Visibility.Visible;
                Bvalue.Visibility = Visibility.Visible;
                BLabel.Visibility = Visibility.Visible;
                HEXValue.Visibility = Visibility.Hidden;
                HEXLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                Rvalue.Visibility = Visibility.Hidden;
                RLabel.Visibility = Visibility.Hidden;
                Gvalue.Visibility = Visibility.Hidden;
                GLabel.Visibility = Visibility.Hidden;
                Bvalue.Visibility = Visibility.Hidden;
                BLabel.Visibility = Visibility.Hidden;
                HEXValue.Visibility = Visibility.Visible;
                HEXLabel.Visibility = Visibility.Visible;
            }
            
        }
    }
}
