// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Models;
using Windows.ApplicationModel.ConversationalAgent;
using Windows.Storage;

namespace ShortcutGuide
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShortcutView : Page
    {
        private ShortcutList shortcutList = YmlInterpreter.GetShortcutsOfDefaultShell();

        public ShortcutView()
        {
            InitializeComponent();

            int i = 0;
            foreach (var category in shortcutList.Shortcuts)
            {
                switch (category.SectionName)
                {
                    case string name when name.StartsWith("<TASKBAR1-9>", System.StringComparison.Ordinal):
                        // Todo: Implement GetTaskbarIconPositions
                        break;
                    case string name when name.StartsWith('<'):
                        break;
                    default:
                        CategorySelector.Items.Add(new SelectorBarItem() { Text = category.SectionName, Name = i.ToString(CultureInfo.InvariantCulture) });
                        break;
                }

                i++;
            }

            CategorySelector.SelectedItem = CategorySelector.Items[0];
            CategorySelector.SelectionChanged += CategorySelector_SelectionChanged;

            foreach (var shortcut in shortcutList.Shortcuts[0].Properties)
            {
                ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
            }

            ShortcutPageParameters.SearchFilter.FilterChanged += SearchFilter_FilterChanged;
        }

        private void SearchFilter_FilterChanged(object? sender, string e)
        {
            FilterBy(e);
        }

        public void FilterBy(string filter)
        {
            ShortcutListElement.Items.Clear();
            foreach (var shortcut in shortcutList.Shortcuts[int.Parse(CategorySelector.SelectedItem.Name, CultureInfo.InvariantCulture)].Properties)
            {
                if (shortcut.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }
            }

            if (ShortcutListElement.Items.Count == 0)
            {
                ShortcutListElement.Items.Add(new ShortcutTemplateDataObject("No search results found", string.Empty, new StackPanel() { Visibility = Microsoft.UI.Xaml.Visibility.Collapsed }));
            }
        }

        public void CategorySelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
        {
            ShortcutListElement.Items.Clear();

            foreach (var shortcut in shortcutList.Shortcuts[int.Parse(sender.SelectedItem.Name, CultureInfo.InvariantCulture)].Properties)
            {
                ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
            }
        }
    }
}
