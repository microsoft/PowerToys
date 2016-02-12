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
    public partial class ResultPanel : UserControl
    {
        public event Action<Result, IDataObject, DragEventArgs> ItemDropEvent;

        public void AddResults(List<Result> newResults, string resultId)
        {
            //lock (_resultsUpdateLock)
            //{
            //    // todo use async to do new result calculation
            //    var resultsCopy = _results.ToList();
            //    var oldResults = resultsCopy.Where(r => r.PluginID == resultId).ToList();
            //    // intersection of A (old results) and B (new newResults)
            //    var intersection = oldResults.Intersect(newResults).ToList();
            //    // remove result of relative complement of B in A
            //    foreach (var result in oldResults.Except(intersection))
            //    {
            //        resultsCopy.Remove(result);
            //    }

            //    // update scores
            //    foreach (var result in newResults)
            //    {
            //        if (IsTopMostResult(result))
            //        {
            //            result.Score = int.MaxValue;
            //        }
            //    }

            //    // update index for result in intersection of A and B
            //    foreach (var commonResult in intersection)
            //    {
            //        int oldIndex = resultsCopy.IndexOf(commonResult);
            //        int oldScore = resultsCopy[oldIndex].Score;
            //        int newScore = newResults[newResults.IndexOf(commonResult)].Score;
            //        if (newScore != oldScore)
            //        {
            //            var oldResult = resultsCopy[oldIndex];
            //            oldResult.Score = newScore;
            //            resultsCopy.RemoveAt(oldIndex);
            //            int newIndex = InsertIndexOf(newScore, resultsCopy);
            //            resultsCopy.Insert(newIndex, oldResult);

            //        }
            //    }

            //    // insert result in relative complement of A in B
            //    foreach (var result in newResults.Except(intersection))
            //    {
            //        int newIndex = InsertIndexOf(result.Score, resultsCopy);
            //        resultsCopy.Insert(newIndex, result);
            //    }

            //    // update UI in one run, so it can avoid UI flickering
            //    _results.Update(resultsCopy);

            //    lbResults.Margin = lbResults.Items.Count > 0 ? new Thickness { Top = 8 } : new Thickness { Top = 0 };
            //    SelectFirst();
            //}
        }
        

        private void UpdateItemNumber()
        {
            //VirtualizingStackPanel virtualizingStackPanel = GetInnerStackPanel(lbResults);
            //int index = 0;
            //for (int i = (int)virtualizingStackPanel.VerticalOffset; i <= virtualizingStackPanel.VerticalOffset + virtualizingStackPanel.ViewportHeight; i++)
            //{
            //    index++;
            //    ListBoxItem item = lbResults.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            //    if (item != null)
            //    {
            //        ContentPresenter myContentPresenter = FindVisualChild<ContentPresenter>(item);
            //        if (myContentPresenter != null)
            //        {
            //            DataTemplate dataTemplate = myContentPresenter.ContentTemplate;
            //            TextBlock tbItemNumber = (TextBlock)dataTemplate.FindName("tbItemNumber", myContentPresenter);
            //            tbItemNumber.Text = index.ToString();
            //        }
            //    }
            //}
        }

        public ResultPanel()
        {
            InitializeComponent();
        }

        private void lbResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                lbResults.ScrollIntoView(e.AddedItems[0]);
                //Dispatcher.DelayInvoke("UpdateItemNumber", () =>
                //{
                    //UpdateItemNumber();
                //}, TimeSpan.FromMilliseconds(3));
            }
        }

        private void ListBoxItem_OnDrop(object sender, DragEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(lbResults, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                OnItemDropEvent(item.DataContext as ResultItemViewModel, e.Data, e);
            }
        }

        protected virtual void OnItemDropEvent(ResultItemViewModel obj, IDataObject data, DragEventArgs e)
        {
            var handler = ItemDropEvent;
            if (handler != null) handler(obj.RawResult, data, e);
        }
    }
}