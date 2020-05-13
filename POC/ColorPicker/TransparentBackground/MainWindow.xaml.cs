using System.Windows;
using System.Windows.Input;

namespace TransparentBackGround
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Cursor = Cursors.Cross;
        }

        public void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
