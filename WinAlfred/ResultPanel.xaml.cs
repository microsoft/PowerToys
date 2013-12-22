using System.Collections.Generic;
using System.Windows.Controls;
using WinAlfred.Plugin;

namespace WinAlfred
{
    /// <summary>
    /// Result.xaml 的交互逻辑
    /// </summary>
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
            foreach (Result result in results)
            {
                ResultItem control = new ResultItem(result);
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
            Height = pnlContainer.Height = results.Count * resultItemHeight;
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
                UnSelectAll();
                var resultItemControl = pnlContainer.Children[index] as ResultItem;
                if (resultItemControl != null) resultItemControl.Selected = true;
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
