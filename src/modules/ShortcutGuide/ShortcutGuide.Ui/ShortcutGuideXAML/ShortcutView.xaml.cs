// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using Windows.Foundation;

namespace ShortcutGuide
{
    public sealed partial class ShortcutView : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _taskbarUpdateTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly bool _showTaskbarShortcuts;
        public static readonly CancellationTokenSource AnimationCancellationTokenSource = new();
        private readonly ShortcutFile _shortcutList = ManifestInterpreter.GetShortcutsOfApplication(ShortcutPageParameters.CurrentPageName);

        public ShortcutView()
        {
            InitializeComponent();
            AnimationCancellationTokenSource.TryReset();
            DataContext = this;

            int i = -1;

            try
            {
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

                CategorySelector.SelectedItem = CategorySelector.Items[0];
                CategorySelector.SelectionChanged += CategorySelector_SelectionChanged;

                foreach (var shortcut in _shortcutList.Shortcuts[0].Properties)
                {
                    ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                }

                ShortcutPageParameters.FrameHeight.FrameHeightChanged += ContentHeightChanged;
                ShortcutPageParameters.SearchFilter.FilterChanged += SearchFilter_FilterChanged;

                if (!ShortcutPageParameters.PinnedShortcuts.TryGetValue(ShortcutPageParameters.CurrentPageName, out var _))
                {
                    ShortcutPageParameters.PinnedShortcuts.Add(ShortcutPageParameters.CurrentPageName, []);
                }

                if (_showTaskbarShortcuts)
                {
                    TaskbarIndicators.Visibility = Visibility.Visible;
                    _taskbarUpdateTimer.Tick += UpdateTaskbarIndicators;
                    _taskbarUpdateTimer.Start();
                }

                OpenOverview();
            }
            catch (Exception)
            {
                OverviewStackPanel.Visibility = Visibility.Collapsed;
                ErrorMessage.Visibility = Visibility.Visible;
                ErrorMessage.Text = ResourceLoaderInstance.ResourceLoader.GetString("ErrorInAppParsing");
            }

            Unloaded += (_, _) =>
            {
                AnimationCancellationTokenSource.Cancel();
                _taskbarUpdateTimer.Tick -= UpdateTaskbarIndicators;
                _taskbarUpdateTimer.Stop();
            };
        }

        private void UpdateTaskbarIndicators(object? sender, object? e)
        {
            NativeMethods.TasklistButton[] buttons = TasklistPositions.GetButtons();
            Canvas[] canvases = [
                TaskbarIndicator1,
                TaskbarIndicator2,
                TaskbarIndicator3,
                TaskbarIndicator4,
                TaskbarIndicator5,
                TaskbarIndicator6,
                TaskbarIndicator7,
                TaskbarIndicator8,
                TaskbarIndicator9,
                TaskbarIndicator10,
            ];

            for (int i = 0; i < canvases.Length; i++)
            {
                if (i < buttons.Length)
                {
                    canvases[i].Visibility = Visibility.Visible;
                    Rect workArea = DisplayHelper.GetWorkAreaForDisplayWithWindow(MainWindow.WindowHwnd);
                    DoubleAnimation animation = new()
                    {
                        To = (buttons[i].X - workArea.Left) / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32()),
                        Duration = TimeSpan.FromMilliseconds(500),
                    };

                    // Create the storyboard
                    Storyboard storyboard = new();
                    storyboard.Children.Add(animation);

                    // Set the target and property
                    Storyboard.SetTarget(animation, canvases[i]);
                    Storyboard.SetTargetProperty(animation, "(Canvas.Left)");

                    // Start the animation
                    storyboard.Begin();

                    canvases[i].Width = buttons[i].Width / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                    canvases[i].Height = buttons[i].Height / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                    ((Rectangle)canvases[i].Children[0]).Width = buttons[i].Width / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                    ((Rectangle)canvases[i].Children[0]).Height = buttons[i].Height / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                    ((TextBlock)canvases[i].Children[1]).Width = buttons[i].Width / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                    ((TextBlock)canvases[i].Children[1]).Height = buttons[i].Height / DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());
                }
                else
                {
                    canvases[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private double _contentHeight;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double ContentHeight
        {
            get => _contentHeight - CategorySelector.ActualHeight;
            set
            {
                _contentHeight = value;
                OnPropertyChanged();
            }
        }

        public void ContentHeightChanged(object? sender, double e)
        {
            ContentHeight = e;
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

            foreach (var list in _shortcutList.Shortcuts)
            {
                foreach (var shortcut in list.Properties)
                {
                    if (shortcut.Recommended)
                    {
                        RecommendedListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                    }
                }
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
                TaskbarLaunchShortcutsListElement.Visibility = Visibility.Visible;
                TaskbarLaunchShortcutsListElement.Items.Clear();
                TaskbarLaunchShortcutsTitle.Visibility = Visibility.Visible;
                foreach (var item in _shortcutList.Shortcuts.First(x => x.SectionName.StartsWith("<TASKBAR1-9>", StringComparison.InvariantCulture)).Properties)
                {
                    TaskbarLaunchShortcutsListElement.Items.Add((ShortcutTemplateDataObject)item);
                }
            }
            else
            {
                TaskbarLaunchShortcutsListElement.Visibility = Visibility.Collapsed;
                TaskbarLaunchShortcutsTitle.Visibility = Visibility.Collapsed;
                TaskbarLaunchShortcutsDescription.Visibility = Visibility.Collapsed;
            }
        }

        private string _searchFilter = string.Empty;

        private void SearchFilter_FilterChanged(object? sender, string e)
        {
            FilterBy(e);
            _searchFilter = e;
        }

        public void FilterBy(string filter)
        {
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

                foreach (var list in _shortcutList.Shortcuts)
                {
                    foreach (var shortcut in list.Properties)
                    {
                        if (shortcut.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
                        {
                            ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                        }
                    }
                }
            }
            else
            {
                foreach (var shortcut in _shortcutList.Shortcuts[int.Parse(CategorySelector.SelectedItem.Name, CultureInfo.InvariantCulture)].Properties)
                {
                    if (shortcut.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
                    {
                        ShortcutListElement.Items.Add((ShortcutTemplateDataObject)shortcut);
                    }
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

        private void PinShortcut(object sender, RoutedEventArgs e)
        {
            if (ShortcutPageParameters.PinnedShortcuts[ShortcutPageParameters.CurrentPageName].Contains(((ShortcutTemplateDataObject)((MenuFlyoutItem)sender).DataContext).OriginalShortcutObject))
            {
                ShortcutPageParameters.PinnedShortcuts[ShortcutPageParameters.CurrentPageName].Remove(((ShortcutTemplateDataObject)((MenuFlyoutItem)sender).DataContext).OriginalShortcutObject);
            }
            else
            {
                ShortcutPageParameters.PinnedShortcuts[ShortcutPageParameters.CurrentPageName].Add(((ShortcutTemplateDataObject)((MenuFlyoutItem)sender).DataContext).OriginalShortcutObject);
            }

            if (int.Parse(CategorySelector.SelectedItem.Name, CultureInfo.InvariantCulture) == -1)
            {
                OpenOverview();
            }

            string serialized = JsonSerializer.Serialize(ShortcutPageParameters.PinnedShortcuts);

            SettingsUtils settingsUtils = new();
            string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
            File.WriteAllText(pinnedPath, serialized);
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout menu ||
                menu.Target is not Grid parentGrid ||
                parentGrid.DataContext is not ShortcutTemplateDataObject dataObject ||
                menu.Items[0] is not MenuFlyoutItem pinItem)
            {
                return;
            }

            ShortcutEntry originalObject = dataObject.OriginalShortcutObject;

            bool isItemPinned = ShortcutPageParameters.PinnedShortcuts[ShortcutPageParameters.CurrentPageName].Any(x => x.Equals(originalObject));

            pinItem.Text = isItemPinned ? ResourceLoaderInstance.ResourceLoader.GetString("UnpinShortcut") : ResourceLoaderInstance.ResourceLoader.GetString("PinShortcut");
            pinItem.Icon = new SymbolIcon(isItemPinned ? Symbol.UnPin : Symbol.Pin);
        }
    }
}
