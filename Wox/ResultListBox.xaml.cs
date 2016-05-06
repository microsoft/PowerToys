using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox
{
    [Synchronization]
    public partial class ResultListBox
    {
        public void AddResults(List<Result> newRawResults)
        {
            var vm = (ResultsViewModel) DataContext;
            var newResults = newRawResults.Select(r => new ResultViewModel(r)).ToList();
            vm.Results.Update(newResults);
            vm.SelectedIndex = 0;
        }
        

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