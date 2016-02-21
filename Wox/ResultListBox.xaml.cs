using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Plugin;
using Wox.Storage;
using Wox.ViewModel;

namespace Wox
{
    [Synchronization]
    public partial class ResultListBox
    {
        public void AddResults(List<Result> newResults, string resultId)
        {
            var vm = DataContext as ResultsViewModel;
            vm.AddResults(newResults, resultId);
        }
        

        public ResultListBox()
        {
            InitializeComponent();
        }

        private void lbResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                ScrollIntoView(e.AddedItems[0]);
            }
        }

    }
}