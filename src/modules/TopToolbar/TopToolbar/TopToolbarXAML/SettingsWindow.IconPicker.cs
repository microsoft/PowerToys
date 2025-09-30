// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Services;

namespace TopToolbar
{
    public sealed partial class SettingsWindow
    {
        private static readonly char[] IconSearchSeparators = { ' ' };

        private async void OnChooseIconFromLibrary(object sender, RoutedEventArgs e)
        {
            var targetButton = (sender as FrameworkElement)?.DataContext as ToolbarButton ?? _vm.SelectedButton;
            if (targetButton == null || Content is not FrameworkElement root)
            {
                return;
            }

            try
            {
                var allItems = BuildIconPickerItems();
                if (allItems.Count == 0)
                {
                    return;
                }

                var dialog = new ContentDialog
                {
                    XamlRoot = root.XamlRoot,
                    Title = "Choose an icon",
                    PrimaryButtonText = "Use icon",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    IsPrimaryButtonEnabled = false,
                    MinWidth = 420,
                };
                dialog.MaxWidth = 640;
                dialog.MaxHeight = 600;

                var iconTemplate = CreateIconPickerTemplate();

                const string itemsPanelXaml = "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ItemsWrapGrid Orientation='Horizontal' MaximumRowsOrColumns='0'/></ItemsPanelTemplate>";
                var itemsPanel = (ItemsPanelTemplate)XamlReader.Load(itemsPanelXaml);

                var gridView = new GridView
                {
                    SelectionMode = ListViewSelectionMode.Single,
                    IsItemClickEnabled = true,
                    ItemTemplate = iconTemplate,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 280,
                    Padding = new Thickness(4, 0, 4, 4),
                };
                gridView.ItemsPanel = itemsPanel;
                ScrollViewer.SetVerticalScrollMode(gridView, ScrollMode.Enabled);
                ScrollViewer.SetVerticalScrollBarVisibility(gridView, ScrollBarVisibility.Auto);
                ScrollViewer.SetHorizontalScrollMode(gridView, ScrollMode.Disabled);

                var emptyState = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false,
                };
                emptyState.Children.Add(new FontIcon
                {
                    Glyph = "\uE737",
                    FontSize = 36,
                    Opacity = 0.35,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                });
                emptyState.Children.Add(new TextBlock
                {
                    Text = "No icons match your search.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.65,
                });

                var contentHost = new Grid();
                contentHost.Children.Add(gridView);
                contentHost.Children.Add(emptyState);

                var layout = new Grid
                {
                    MinWidth = 420,
                    MaxWidth = 640,
                    MinHeight = 360,
                    MaxHeight = 520,
                    RowSpacing = 8,
                };

                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var searchBox = new AutoSuggestBox
                {
                    PlaceholderText = "Search icons",
                    Margin = new Thickness(0, 0, 0, 4),
                    QueryIcon = new SymbolIcon(Symbol.Find),
                };

                searchBox.Loaded += (s, _) => searchBox.Focus(FocusState.Programmatic);

                Grid.SetRow(searchBox, 0);
                layout.Children.Add(searchBox);

                Grid.SetRow(contentHost, 1);
                layout.Children.Add(contentHost);

                dialog.Content = layout;

                IconPickerItem selectedItem = DetermineInitialSelection(targetButton, allItems);
                string selectedId = selectedItem?.Id;
                List<IconPickerItem> filteredItems = new(allItems);

                void ApplySelection(IconPickerItem item, bool updateView = true)
                {
                    if (item == null)
                    {
                        selectedItem = null;
                        selectedId = null;

                        if (updateView)
                        {
                            gridView.SelectedIndex = -1;
                        }

                        dialog.IsPrimaryButtonEnabled = false;
                        return;
                    }

                    selectedId = item.Id;
                    selectedItem = allItems.FirstOrDefault(candidate => candidate.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase)) ?? item;

                    if (updateView)
                    {
                        var index = filteredItems.FindIndex(candidate => candidate.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase));
                        if (index >= 0)
                        {
                            gridView.SelectedIndex = index;
                            gridView.UpdateLayout();
                            gridView.ScrollIntoView(filteredItems[index], ScrollIntoViewAlignment.Leading);
                        }
                    }

                    dialog.IsPrimaryButtonEnabled = true;
                }

                List<IconPickerItem> FilterItems(string query)
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return new List<IconPickerItem>(allItems);
                    }

                    var tokens = query.Split(IconSearchSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (tokens.Length == 0)
                    {
                        return new List<IconPickerItem>(allItems);
                    }

                    return allItems.Where(item => tokens.All(item.MatchesTerm)).ToList();
                }

                void RefreshFilteredItems(string query, bool scrollToSelection)
                {
                    filteredItems = FilterItems(query);
                    gridView.ItemsSource = filteredItems;

                    var hasItems = filteredItems.Count > 0;
                    gridView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
                    emptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

                    if (!hasItems)
                    {
                        ApplySelection(null, updateView: false);
                        return;
                    }

                    if (!string.IsNullOrEmpty(selectedId))
                    {
                        var match = filteredItems.FirstOrDefault(candidate => candidate.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            ApplySelection(match, updateView: scrollToSelection);
                            return;
                        }
                    }

                    gridView.SelectedIndex = -1;
                    dialog.IsPrimaryButtonEnabled = false;
                }

                gridView.SelectionChanged += (s, args) =>
                {
                    if (gridView.SelectedItem is IconPickerItem item)
                    {
                        ApplySelection(item, updateView: false);
                    }
                    else
                    {
                        ApplySelection(null, updateView: false);
                    }
                };

                gridView.ItemClick += (s, args) =>
                {
                    if (args.ClickedItem is IconPickerItem item)
                    {
                        ApplySelection(item, updateView: false);
                        ApplyIconSelection(targetButton, item);
                        AppLogger.LogInfo($"Icon picker quick select: {item.DisplayName} [{item.IconType}] id={item.Id}");
                        try
                        {
                            dialog.Hide();
                        }
                        catch (Exception hideEx)
                        {
                            AppLogger.LogWarning($"Icon picker dialog hide failed: {hideEx.Message}");
                        }
                    }
                };

                searchBox.TextChanged += (s, args) =>
                {
                    if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
                    {
                        return;
                    }

                    RefreshFilteredItems(searchBox.Text, scrollToSelection: false);
                };

                searchBox.QuerySubmitted += (s, args) =>
                {
                    RefreshFilteredItems(args.QueryText ?? searchBox.Text, scrollToSelection: true);
                };

                RefreshFilteredItems(searchBox.Text, scrollToSelection: true);

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && selectedItem != null)
                {
                    AppLogger.LogInfo($"Icon picker primary select: {selectedItem.DisplayName} [{selectedItem.IconType}] id={selectedItem.Id}");
                    ApplyIconSelection(targetButton, selectedItem);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OnChooseIconFromLibrary failed", ex);
            }
        }

        private void ApplyIconSelection(ToolbarButton button, IconPickerItem item)
        {
            if (button == null || item == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(item.CatalogId))
            {
                _vm.TrySetCatalogIcon(button, item.CatalogId);
            }
            else if (!string.IsNullOrWhiteSpace(item.ImagePath))
            {
                _vm.TrySetImageIcon(button, item.ImagePath);
            }
        }

        private IconPickerItem DetermineInitialSelection(ToolbarButton button, List<IconPickerItem> allItems)
        {
            if (button == null)
            {
                return null;
            }

            if (IconCatalogService.TryParseCatalogId(button.IconPath, out var catalogId))
            {
                return allItems.FirstOrDefault(item => string.Equals(item.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private List<IconPickerItem> BuildIconPickerItems()
        {
            return IconCatalogService.GetAll()
                .Where(entry => entry != null)
                .Select(IconPickerItem.FromCatalog)
                .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static DataTemplate CreateIconPickerTemplate()
        {
            const string templateXaml = @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                                  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                                  xmlns:media='using:Microsoft.UI.Xaml.Media.Imaging'>
                <StackPanel Width='96'
                            Height='120'
                            HorizontalAlignment='Center'
                            Spacing='6'>
                    <Border Width='72'
                            Height='72'
                            CornerRadius='18'
                            Background='{ThemeResource CardBackgroundFillColorDefaultBrush}'
                            BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}'
                            BorderThickness='1'>
                        <Grid>
                            <Image Width='48'
                                   Height='48'
                                   HorizontalAlignment='Center'
                                   VerticalAlignment='Center'
                                   Stretch='Uniform'
                                   Visibility='{Binding SvgVisibility}'>
                                <Image.Source>
                                    <media:SvgImageSource UriSource='{Binding ImageUri}'/>
                                </Image.Source>
                            </Image>
                            <FontIcon Glyph='{Binding Glyph}'
                                      FontFamily='Segoe Fluent Icons,Segoe MDL2 Assets'
                                      FontSize='28'
                                      HorizontalAlignment='Center'
                                      VerticalAlignment='Center'
                                      Foreground='{ThemeResource TextFillColorPrimaryBrush}'
                                      Visibility='{Binding GlyphVisibility}' />
                        </Grid>
                    </Border>
                    <TextBlock Text='{Binding DisplayName}'
                               TextAlignment='Center'
                               FontWeight='SemiBold'
                               TextWrapping='WrapWholeWords'/>
                    <TextBlock Text='{Binding Category}'
                               TextAlignment='Center'
                               FontSize='10'
                               Opacity='0.6'
                               TextWrapping='WrapWholeWords'/>
                </StackPanel>
            </DataTemplate>";

            return (DataTemplate)XamlReader.Load(templateXaml);
        }

        private sealed class IconPickerItem
        {
            private IconPickerItem(string id, string displayName, string category, ToolbarIconType iconType, Uri imageUri, string glyph, string catalogId, string imagePath, IReadOnlyList<string> keywords)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                IconType = iconType;
                ImageUri = imageUri;
                Glyph = glyph;
                CatalogId = catalogId;
                ImagePath = imagePath;
                Keywords = keywords ?? Array.Empty<string>();
            }

            public string Id { get; }

            public string DisplayName { get; }

            public string Category { get; }

            public ToolbarIconType IconType { get; }

            public Uri ImageUri { get; }

            public string Glyph { get; }

            public string CatalogId { get; }

            public string ImagePath { get; }

            public IReadOnlyList<string> Keywords { get; }

            public Visibility SvgVisibility => ImageUri != null ? Visibility.Visible : Visibility.Collapsed;

            public Visibility GlyphVisibility => !string.IsNullOrWhiteSpace(Glyph) ? Visibility.Visible : Visibility.Collapsed;

            public bool MatchesTerm(string term)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return true;
                }

                var comparison = StringComparison.OrdinalIgnoreCase;

                bool Contains(string source) => !string.IsNullOrWhiteSpace(source) && source.Contains(term, comparison);

                if (Contains(DisplayName) || Contains(Category) || Contains(Glyph) || Contains(CatalogId))
                {
                    return true;
                }

                if (Keywords != null)
                {
                    foreach (var keyword in Keywords)
                    {
                        if (Contains(keyword))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public static IconPickerItem FromCatalog(IconCatalogEntry entry)
            {
                return new IconPickerItem(
                    id: "catalog-" + entry.Id,
                    displayName: entry.DisplayName,
                    category: string.IsNullOrWhiteSpace(entry.Category) ? "Catalog" : entry.Category,
                    iconType: ToolbarIconType.Catalog,
                    imageUri: entry.ResourceUri,
                    glyph: entry.Glyph,
                    catalogId: entry.Id,
                    imagePath: string.Empty,
                    keywords: entry.Keywords ?? Array.Empty<string>());
            }
        }
    }
}
