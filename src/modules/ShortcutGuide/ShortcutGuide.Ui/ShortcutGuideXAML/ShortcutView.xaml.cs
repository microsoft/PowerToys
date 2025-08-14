// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.ShortcutGuideXAML;
using WinUIEx;
using Grid = Microsoft.UI.Xaml.Controls.Grid;

namespace ShortcutGuide
{
    public sealed partial class ShortcutView
    {
        private readonly DispatcherTimer _taskbarIconsUpdateTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly ShortcutFile _shortcutList = ManifestInterpreter.GetShortcutsOfApplication(ShortcutPageParameters.CurrentPageName);
        private bool _showTaskbarShortcuts;

        private static CancellationTokenSource _animationCancellationTokenSource = new();

        /// <summary>
        /// Gets or sets a cancellation token source for animations in shortcut view.
        /// When setting a new token source, the previous one is cancelled to stop ongoing animations.
        /// </summary>
        public static CancellationTokenSource AnimationCancellationTokenSource
        {
            get => _animationCancellationTokenSource;
            set
            {
                _animationCancellationTokenSource?.Cancel();
                _animationCancellationTokenSource = value;
            }
        }

        public ShortcutView()
        {
            InitializeComponent();
            DataContext = this;

            try
            {
                PopulateCategorySelector();

                CategorySelector.SelectedItem = CategorySelector.Items[0];
                CategorySelector.SelectionChanged += CategorySelector_SelectionChanged;

                foreach (var shortcut in _shortcutList.Shortcuts[0].Properties)
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }

                ShortcutPageParameters.SearchFilter.FilterChanged += SearchFilter_FilterChanged;

                if (!ShortcutPageParameters.PinnedShortcuts.TryGetValue(ShortcutPageParameters.CurrentPageName, out var _))
                {
                    ShortcutPageParameters.PinnedShortcuts.Add(ShortcutPageParameters.CurrentPageName, []);
                }

                if (_showTaskbarShortcuts)
                {
                    App.TaskBarWindow.Activate();
                    _taskbarIconsUpdateTimer.Start();
                }

                OpenOverview();
            }
            catch (Exception)
            {
                OverviewStackPanel.Visibility = Visibility.Collapsed;
                ErrorMessage.Visibility = Visibility.Visible;
                ErrorMessage.Text = ResourceLoaderInstance.ResourceLoader.GetString("ErrorInAppParsing");
            }
        }

        /// <summary>
        /// Populates the <see cref="CategorySelector"/> selector and sets <see cref="_showTaskbarShortcuts"/>.
        /// </summary>
        private void PopulateCategorySelector()
        {
            int i = -1;
            CategorySelector.Items.Add(new SelectorBarItem()
            {
                Text = ResourceLoaderInstance.ResourceLoader.GetString("Overview"),
                Name = i.ToString(CultureInfo.InvariantCulture),
            });

            i++;

            foreach (var category in _shortcutList.Shortcuts)
            {
                switch (category.SectionName)
                {
                    case { } name when name.StartsWith("<TASKBAR1-9>", StringComparison.Ordinal):
                        _showTaskbarShortcuts = true;
                        break;
                    case { } name when name.StartsWith('<') && name.EndsWith('>'):
                        break;
                    default:
                        CategorySelector.Items.Add(new SelectorBarItem() { Text = category.SectionName, Name = i.ToString(CultureInfo.InvariantCulture) });
                        break;
                }

                i++;
            }
        }

        private void OpenOverview()
        {
            RecommendedListElement.Items.Clear();
            PinnedListElement.Items.Clear();
            OverviewStackPanel.Visibility = Visibility.Visible;
            RecommendedListElement.Visibility = Visibility.Visible;
            PinnedListElement.Visibility = Visibility.Visible;
            RecommendedListTitle.Visibility = Visibility.Visible;
            PinnedListTitle.Visibility = Visibility.Visible;
            ShortcutListElement.Visibility = Visibility.Collapsed;

            foreach (var shortcut in _shortcutList.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended)))
            {
                RecommendedListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
            }

            if (RecommendedListElement.Items.Count == 0)
            {
                RecommendedListTitle.Visibility = Visibility.Collapsed;
                RecommendedListElement.Visibility = Visibility.Collapsed;
            }

            foreach (var shortcut in ShortcutPageParameters.PinnedShortcuts[ShortcutPageParameters.CurrentPageName])
            {
                PinnedListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
            }

            if (PinnedListElement.Items.Count == 0)
            {
                PinnedListTitle.Visibility = Visibility.Collapsed;
                PinnedListElement.Visibility = Visibility.Collapsed;
            }

            if (RecommendedListElement.Items.Count == 0 && PinnedListElement.Items.Count == 0)
            {
                OverviewStackPanel.Visibility = Visibility.Collapsed;
                ErrorMessage.Visibility = Visibility.Visible;
                ErrorMessage.Text = ResourceLoaderInstance.ResourceLoader.GetString("NoShortcutsInOverview");
            }

            if (_showTaskbarShortcuts)
            {
                App.TaskBarWindow.Activate();
                TaskbarLaunchShortcutsListElement.Visibility = Visibility.Visible;
                TaskbarLaunchShortcutsListElement.Items.Clear();
                TaskbarLaunchShortcutsTitle.Visibility = Visibility.Visible;
                foreach (var item in _shortcutList.Shortcuts.First(x => x.SectionName.StartsWith("<TASKBAR1-9>", StringComparison.InvariantCulture)).Properties)
                {
                    TaskbarLaunchShortcutsListElement.Items.Add((ShortcutTemplateDataObject)item);
                }

                return;
            }

            TaskbarLaunchShortcutsListElement.Visibility = Visibility.Collapsed;
            TaskbarLaunchShortcutsTitle.Visibility = Visibility.Collapsed;
            TaskbarLaunchShortcutsDescription.Visibility = Visibility.Collapsed;
        }

        private string _searchFilter = string.Empty;

        private void SearchFilter_FilterChanged(object? sender, string e)
        {
            FilterBy(e);
            _searchFilter = e;
        }

        public void FilterBy(string filter)
        {
            App.TaskBarWindow.Hide();
            ShortcutsScrollViewer.Margin = new Thickness(0);
            ShortcutListElement.Items.Clear();
            ShortcutListElement.Visibility = Visibility.Visible;
            ErrorMessage.Visibility = Visibility.Collapsed;

            if (int.Parse(CategorySelector.SelectedItem.Name, CultureInfo.InvariantCulture) == -1)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    OpenOverview();
                    return;
                }

                OverviewStackPanel.Visibility = Visibility.Collapsed;
                foreach (var shortcut in _shortcutList.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase))))
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }
            }
            else
            {
                foreach (var shortcut in _shortcutList.Shortcuts[int.Parse(CategorySelector.SelectedItem.Name, CultureInfo.InvariantCulture)].Properties.Where(s => s.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase)))
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }
            }

            if (ShortcutListElement.Items.Count != 0)
            {
                return;
            }

            ShortcutListElement.Visibility = Visibility.Collapsed;
            ErrorMessage.Visibility = Visibility.Visible;
            ErrorMessage.Text = ResourceLoaderInstance.ResourceLoader.GetString("SearchBlank");
        }

        public void CategorySelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
        {
            ShortcutListElement.Items.Clear();
            RecommendedListElement.Items.Clear();
            PinnedListElement.Items.Clear();
            TaskbarLaunchShortcutsListElement.Items.Clear();
            ErrorMessage.Visibility = Visibility.Collapsed;
            RecommendedListElement.Visibility = Visibility.Collapsed;
            PinnedListElement.Visibility = Visibility.Collapsed;
            OverviewStackPanel.Visibility = Visibility.Collapsed;
            TaskbarLaunchShortcutsListElement.Visibility = Visibility.Collapsed;
            TaskbarLaunchShortcutsTitle.Visibility = Visibility.Collapsed;
            TaskbarLaunchShortcutsDescription.Visibility = Visibility.Collapsed;
            ShortcutListElement.Visibility = Visibility.Visible;
            App.TaskBarWindow.Hide();
            ShortcutsScrollViewer.Margin = new Thickness(0);

            try
            {
                if (int.Parse(sender.SelectedItem.Name, CultureInfo.InvariantCulture) == -1)
                {
                    OpenOverview();
                    FilterBy(_searchFilter);
                    return;
                }

                foreach (var shortcut in _shortcutList.Shortcuts[int.Parse(sender.SelectedItem.Name, CultureInfo.InvariantCulture)].Properties)
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }
            }
            catch (NullReferenceException)
            {
                ErrorMessage.Visibility = Visibility.Visible;
                ErrorMessage.Text = ResourceLoaderInstance.ResourceLoader.GetString("ErrorInCategoryParsing");
            }

            FilterBy(_searchFilter);
        }
    }
}
