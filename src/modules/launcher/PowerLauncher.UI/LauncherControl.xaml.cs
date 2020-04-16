using Windows.UI.Xaml.Controls;

namespace PowerLauncher.UI
{
    public sealed partial class LauncherControl : UserControl
    {
        //private Point _lastpos;
        private ListBoxItem curItem = null;

        public LauncherControl()
        {
            InitializeComponent();
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                //ScrollIntoViewAlignment(e.AddedItems[0]);
            }
        }
    }
}