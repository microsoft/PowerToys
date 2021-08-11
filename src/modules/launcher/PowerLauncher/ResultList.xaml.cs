// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PowerLauncher
{
    /// <summary>
    /// Interaction logic for ResultList.xaml
    /// </summary>
    public partial class ResultList : UserControl
    {
        public ResultList()
        {
            InitializeComponent();
        }

        private ToolTip _previouslyOpenedToolTip;

        // From https://docs.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-find-datatemplate-generated-elements
        private TChildItem FindVisualChild<TChildItem>(DependencyObject obj)
    where TChildItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is TChildItem)
                {
                    return (TChildItem)child;
                }
                else
                {
                    TChildItem childOfChild = FindVisualChild<TChildItem>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }

            return null;
        }

        private void HideCurrentToolTip()
        {
            if (_previouslyOpenedToolTip != null)
            {
                _previouslyOpenedToolTip.IsOpen = false;
            }
        }

        private void ContextMenuListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectedItem != null)
            {
                ListBoxItem listBoxItem = (ListBoxItem)listView.ItemContainerGenerator.ContainerFromItem(listView.SelectedItem);
                ContentPresenter contentPresenter = FindVisualChild<ContentPresenter>(listBoxItem);
                DataTemplate dataTemplate = contentPresenter.ContentTemplate;
                Button button = (Button)dataTemplate.FindName("commandButton", contentPresenter);
                ToolTip tooltip = button.ToolTip as ToolTip;
                tooltip.PlacementTarget = button;
                tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                tooltip.PlacementRectangle = new Rect(0, button.Height, 0, 0);
                tooltip.IsOpen = true;
            }
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            if (string.Equals(sender.GetType().FullName, "System.Windows.Controls.ToolTip", System.StringComparison.InvariantCulture))
            {
                HideCurrentToolTip();
                _previouslyOpenedToolTip = (ToolTip)sender;
            }
        }

        private void SuggestionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.Equals(((ListView)e.OriginalSource).Name, "SuggestionsList", System.StringComparison.InvariantCulture))
            {
                HideCurrentToolTip();
            }
        }
    }
}
