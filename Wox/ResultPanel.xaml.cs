using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wox.Helper;
using Wox.Plugin;
using Wox.Storage;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox
{
    public partial class ResultPanel : UserControl
    {
        public event Action<Result> LeftMouseClickEvent;
        public event Action<Result> RightMouseClickEvent;
        public event Action<Result, IDataObject, DragEventArgs> ItemDropEvent;

        protected virtual void OnRightMouseClick(Result result)
        {
            Action<Result> handler = RightMouseClickEvent;
            if (handler != null) handler(result);
        }

        protected virtual void OnLeftMouseClick(Result result)
        {
            Action<Result> handler = LeftMouseClickEvent;
            if (handler != null) handler(result);
        }

        public bool Dirty { get; set; }

        public void AddResults(List<Result> results)
        {
            if (Dirty)
            {
                Dirty = false;
                lbResults.Items.Clear();
            }
            foreach (var result in results)
            {
                int position = 0;
                if (IsTopMostResult(result))
                {
                    result.Score = int.MaxValue;
                }
                else
                {
                    if (result.Score >= int.MaxValue)
                    {
                        result.Score = int.MaxValue - 1;
                    }
                    position = GetInsertLocation(result.Score);
                }
                lbResults.Items.Insert(position, result);
            }
            lbResults.Margin = lbResults.Items.Count > 0 ? new Thickness { Top = 8 } : new Thickness { Top = 0 };
            SelectFirst();
        }

        private bool IsTopMostResult(Result result)
        {
            return TopMostRecordStorage.Instance.IsTopMost(result);
        }

        private int GetInsertLocation(int currentScore)
        {
            int location = lbResults.Items.Count;
            if (lbResults.Items.Count == 0) return 0;
            if (currentScore > ((Result)lbResults.Items[0]).Score) return 0;

            for (int index = 1; index < lbResults.Items.Count; index++)
            {
                Result next = lbResults.Items[index] as Result;
                Result prev = lbResults.Items[index - 1] as Result;
                if (next != null && prev != null)
                {
                    if ((currentScore >= next.Score && currentScore <= prev.Score))
                    {
                        if (currentScore == next.Score)
                        {
                            location = index + 1;
                        }
                        else
                        {
                            location = index;
                        }
                    }
                }
            }

            return location;
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
            VirtualizingStackPanel virtualizingStackPanel = GetInnerStackPanel(lbResults);
            int index = 0;
            for (int i = (int)virtualizingStackPanel.VerticalOffset; i <= virtualizingStackPanel.VerticalOffset + virtualizingStackPanel.ViewportHeight; i++)
            {
                index++;
                ListBoxItem item = lbResults.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (item != null)
                {
                    ContentPresenter myContentPresenter = FindVisualChild<ContentPresenter>(item);
                    if (myContentPresenter != null)
                    {
                        DataTemplate dataTemplate = myContentPresenter.ContentTemplate;
                        TextBlock tbItemNumber = (TextBlock)dataTemplate.FindName("tbItemNumber", myContentPresenter);
                        tbItemNumber.Text = index.ToString();
                    }
                }
            }
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

        public void Clear()
        {
            lbResults.Items.Clear();
            lbResults.Margin = new Thickness { Top = 0 };
        }

        private void lbResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                lbResults.ScrollIntoView(e.AddedItems[0]);
                Dispatcher.DelayInvoke("UpdateItemNumber", o =>
                {
                    UpdateItemNumber();
                }, TimeSpan.FromMilliseconds(3));
            }
        }

        private void LbResults_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(lbResults, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null && e.ChangedButton == MouseButton.Left)
            {
                OnLeftMouseClick(item.DataContext as Result);
            }
            if (item != null && e.ChangedButton == MouseButton.Right)
            {
                OnRightMouseClick(item.DataContext as Result);
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
                OnItemDropEvent(item.DataContext as Result, e.Data, e);
            }
        }

        protected virtual void OnItemDropEvent(Result obj, IDataObject data, DragEventArgs e)
        {
            var handler = ItemDropEvent;
            if (handler != null) handler(obj, data, e);
        }
    }
}