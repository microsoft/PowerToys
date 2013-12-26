using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WinAlfred.Plugin;

namespace WinAlfred
{
    public partial class ResultPanel : UserControl
    {
        public delegate void ResultItemsChanged();

        public event ResultItemsChanged resultItemChangedEvent;

        protected virtual void OnResultItemChangedEvent()
        {
            ResultItemsChanged handler = resultItemChangedEvent;
            if (handler != null) handler();
        }

        public void AddResults(List<Result> results)
        {
            pnlContainer.Children.Clear();
            for (int i = 0; i < results.Count; i++)
            {
                Result result = results[i];
                ResultItem control = new ResultItem(result);
                control.SetIndex(i + 1);
                pnlContainer.Children.Add(control);
            }

            pnlContainer.UpdateLayout();

            double resultItemHeight = 0;
            if (pnlContainer.Children.Count > 0)
            {
                var resultItem = pnlContainer.Children[0] as ResultItem;
                if (resultItem != null)
                    resultItemHeight = resultItem.ActualHeight;
            }
            pnlContainer.Height = results.Count * resultItemHeight;
            OnResultItemChangedEvent();
        }

        private int GetCurrentSelectedResultIndex()
        {
            for (int i = 0; i < pnlContainer.Children.Count; i++)
            {
                var resultItemControl = pnlContainer.Children[i] as ResultItem;
                if (resultItemControl != null && resultItemControl.Selected)
                {
                    return i;
                }
            }
            return -1;
        }

        public void UnSelectAll()
        {
            for (int i = 0; i < pnlContainer.Children.Count; i++)
            {
                var resultItemControl = pnlContainer.Children[i] as ResultItem;
                if (resultItemControl != null && resultItemControl.Selected)
                {
                    resultItemControl.Selected = false;
                }
            }
        }

        public void SelectNext()
        {
            int index = GetCurrentSelectedResultIndex();
            if (index == pnlContainer.Children.Count - 1)
            {
                index = -1;
            }
            Select(index + 1);
        }

        public void SelectPrev()
        {
            int index = GetCurrentSelectedResultIndex();
            if (index == 0)
            {
                index = pnlContainer.Children.Count;
            }
            Select(index - 1);
        }

        private void Select(int index)
        {
            if (pnlContainer.Children.Count > 0)
            {
                int oldIndex = GetCurrentSelectedResultIndex();

                UnSelectAll();
                var resultItemControl = pnlContainer.Children[index] as ResultItem;
                if (resultItemControl != null)
                {
                    resultItemControl.Selected = true;

                    double scrollPosition = 0;
                    Point newItemBottomPoint = resultItemControl.TranslatePoint(new Point(0, resultItemControl.ActualHeight), pnlContainer);
                    if (index == 0)
                    {
                        sv.ScrollToTop();
                        return;
                    }
                    if (index == pnlContainer.Children.Count - 1)
                    {
                        sv.ScrollToBottom();
                        return;
                    }

                    if (index < oldIndex)
                    {
                        //move up and old item is at the top of the scroll view 
                        if ( newItemBottomPoint.Y - sv.VerticalOffset == 0)
                        {
                            scrollPosition = sv.VerticalOffset - resultItemControl.ActualHeight;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        //move down and old item is at the bottom of scroll view
                        if (sv.ActualHeight + sv.VerticalOffset == newItemBottomPoint.Y - resultItemControl.ActualHeight)
                        {
                            scrollPosition = newItemBottomPoint.Y - sv.ActualHeight;
                        }
                        else
                        {
                            return;
                        }
                    }

                    sv.ScrollToVerticalOffset(scrollPosition);
                }
            }
        }

        public void SelectFirst()
        {
            Select(0);
        }

        public void AcceptSelect()
        {
            int index = GetCurrentSelectedResultIndex();
            var resultItemControl = pnlContainer.Children[index] as ResultItem;
            if (resultItemControl != null)
            {
                resultItemControl.Result.Action();
            }
        }

        public ResultPanel()
        {
            InitializeComponent();
        }
    }
}