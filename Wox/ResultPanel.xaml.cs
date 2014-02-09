using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Helper;
using Wox.Plugin;

namespace Wox
{
    public partial class ResultPanel : UserControl
    {
        public bool Dirty { get; set; }

        public delegate void ResultItemsChanged();

        public event ResultItemsChanged resultItemChangedEvent;

        protected virtual void OnResultItemChangedEvent()
        {
            ResultItemsChanged handler = resultItemChangedEvent;
            if (handler != null) handler();
        }

        public void AddResults(List<Result> results)
        {
            if (results.Count == 0) return;

            if (Dirty)
            {
                Dirty = false;
                pnlContainer.Children.Clear();
            }

            for (int i = 0; i < results.Count; i++)
            {
                Result result = results[i];
                if (!CheckExisted(result))
                {
                    ResultItem control = new ResultItem(result);
                    pnlContainer.Children.Insert(GetInsertLocation(result.Score), control);
                }
            }

            SelectFirst();
            pnlContainer.UpdateLayout();

            double resultItemHeight = 0;
            if (pnlContainer.Children.Count > 0)
            {
                var resultItem = pnlContainer.Children[0] as ResultItem;
                if (resultItem != null)
                    resultItemHeight = resultItem.ActualHeight;
            }
            pnlContainer.Height = pnlContainer.Children.Count * resultItemHeight;
            OnResultItemChangedEvent();
        }

        private bool CheckExisted(Result result)
        {
            return pnlContainer.Children.Cast<ResultItem>().Any(child => child.Result.Equals(result));
        }

        private int GetInsertLocation(int currentScore)
        {
            int location = pnlContainer.Children.Count;
            if (pnlContainer.Children.Count == 0) return 0;
            if (currentScore > ((ResultItem)pnlContainer.Children[0]).Result.Score) return 0;

            for (int index = 1; index < pnlContainer.Children.Count; index++)
            {
                ResultItem next = pnlContainer.Children[index] as ResultItem;
                ResultItem prev = pnlContainer.Children[index - 1] as ResultItem;
                if (next != null && prev != null)
                {
                    if ((currentScore >= next.Result.Score && currentScore <= prev.Result.Score))
                    {
                        if (currentScore == next.Result.Score)
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

        public int GetCurrentResultCount()
        {
            return pnlContainer.Children.Count;
        }

        public int GetCurrentSelectedResultIndex()
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
                    scrollPosition = newItemBottomPoint.Y;
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
                        var scrollPostionY = sv.VerticalOffset - sv.VerticalOffset%resultItemControl.ActualHeight +
                                             resultItemControl.ActualHeight;
                        if (newItemBottomPoint.Y - scrollPostionY == 0)
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
                        double scrollPostionY = (sv.ActualHeight + sv.VerticalOffset) - (sv.ActualHeight + sv.VerticalOffset)%resultItemControl.ActualHeight;
                        if (scrollPostionY  == newItemBottomPoint.Y - resultItemControl.ActualHeight)
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

        public Result AcceptSelect()
        {
            int index = GetCurrentSelectedResultIndex();
            if (index < 0) return null;

            var resultItemControl = pnlContainer.Children[index] as ResultItem;
            if (resultItemControl != null)
            {
                if (resultItemControl.Result.Action != null)
                {
                    resultItemControl.Result.Action(new ActionContext()
                    {
                        SpecialKeyState = new KeyboardListener().CheckModifiers()
                    });
                }

                return resultItemControl.Result;
            }

            return null;
        }

        public ResultPanel()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            pnlContainer.Children.Clear();
            pnlContainer.Height = 0;
            OnResultItemChangedEvent();
        }
    }
}