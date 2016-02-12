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
        //TODO: Refactor this event
        public event Action<Result, IDataObject, DragEventArgs> ItemDropEvent;

        public void AddResults(List<Result> newResults, string resultId)
        {
            var vm = this.DataContext as ResultPanelViewModel;
            vm.AddResults(newResults, resultId);
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