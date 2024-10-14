// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Models;

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
                CategorySelector.Items.Add(new SelectorBarItem() { Text = category.SectionName, Name = i.ToString(CultureInfo.InvariantCulture) });
                i++;
            }

            CategorySelector.SelectedItem = CategorySelector.Items[0];
            CategorySelector.SelectionChanged += CategorySelector_SelectionChanged;

            foreach (var shortcut in shortcutList.Shortcuts[0].Properties)
            {
                ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
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
