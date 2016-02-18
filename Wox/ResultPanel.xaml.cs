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
        private readonly object _resultsUpdateLock = new object();

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
        

        public void SelectNext()
        {
            int index = lbResults.SelectedIndex;
            if (index == lbResults.Items.Count - 1)
            {
                index = -1;
            }
            Select(index + 1);
        }

        public void SelectPrev()
        {
            int index = lbResults.SelectedIndex;
            if (index == 0)
            {
                index = lbResults.Items.Count;
            }
            Select(index - 1);
        }

        private void SelectFirst()
        {
            Select(0);
        }

        private void Select(int index)
        {
            if (index >= 0 && index < lbResults.Items.Count)
            {
                lbResults.SelectedItem = lbResults.Items.GetItemAt(index);
            }
        }

        public List<Result> GetVisibleResults()
        {
            List<Result> visibleElements = new List<Result>();
            VirtualizingStackPanel virtualizingStackPanel = GetInnerStackPanel(lbResults);
            for (int i = (int)virtualizingStackPanel.VerticalOffset; i <= virtualizingStackPanel.VerticalOffset + virtualizingStackPanel.ViewportHeight; i++)
            {
                ListBoxItem item = lbResults.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (item != null)
                {
                    visibleElements.Add(item.DataContext as Result);
                }
            }
            return visibleElements;
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

        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private VirtualizingStackPanel GetInnerStackPanel(FrameworkElement element)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;

                if (child == null) continue;

                if (child is VirtualizingStackPanel) return child as VirtualizingStackPanel;

                var panel = GetInnerStackPanel(child);

                if (panel != null)
                    return panel;
            }

            return null;

        }

        public Result GetActiveResult()
        {
            int index = lbResults.SelectedIndex;
            if (index < 0) return null;

            return lbResults.Items[index] as Result;
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

        public void SelectNextPage()
        {
            int index = lbResults.SelectedIndex;
            index += 5;
            if (index >= lbResults.Items.Count)
            {
                index = lbResults.Items.Count - 1;
            }
            Select(index);
        }

        public void SelectPrevPage()
        {
            int index = lbResults.SelectedIndex;
            index -= 5;
            if (index < 0)
            {
                index = 0;
            }
            Select(index);
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