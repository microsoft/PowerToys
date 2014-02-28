using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Wox
{
    public partial class ResultPanel : UserControl
    {
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
                int position = GetInsertLocation(result.Score);
                lbResults.Items.Insert(position, result);
            }
            gridContainer.Margin = lbResults.Items.Count > 0 ? new Thickness { Top = 8 } : new Thickness { Top = 0 };
            SelectFirst();
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

        private FrameworkElement FindByName(string name, FrameworkElement root)
        {
            Stack<FrameworkElement> tree = new Stack<FrameworkElement>();
            tree.Push(root);

            while (tree.Count > 0)
            {
                FrameworkElement current = tree.Pop();
                if (current.Name == name)
                    return current;

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; ++i)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(current, i);
                    if (child is FrameworkElement)
                        tree.Push((FrameworkElement)child);
                }
            }

            return null;
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

        public Result AcceptSelect()
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
            gridContainer.Margin = new Thickness { Top = 0 };
        }

        private void lbResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                lbResults.ScrollIntoView(e.AddedItems[0]);
            }
        }
    }
}