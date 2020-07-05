using ColorPicker.ViewModelContracts;
using System.ComponentModel.Composition;
using System.Windows;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Closing += MainWindow_Closing;
            Bootstrapper.InitializeContainer(this);
            InitializeComponent();
            DataContext = this;
            Hide();
        }


        [Import]
        public IMainViewModel MainViewModel { get; set; }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Closing -= MainWindow_Closing;
            Bootstrapper.Dispose();
        }
    }
}
