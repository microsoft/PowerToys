using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {
        public TransparentWindow()
        {
            InitializeComponent();
            this.Cursor = Cursors.Cross;
        }
    }
}
