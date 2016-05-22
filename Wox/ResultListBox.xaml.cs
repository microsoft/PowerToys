using System.Runtime.Remoting.Contexts;
using System.Windows.Controls;
using System.Windows.Input;

namespace Wox
{
    [Synchronization]
    public partial class ResultListBox
    {
        public ResultListBox()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                ScrollIntoView(e.AddedItems[0]);
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            ((ListBoxItem) sender).IsSelected = true;
        }
    }
}