// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.AdaptiveCards;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveListInputControl : AdaptiveListInputControlBase
{
    private const string FileGlyph = "\uE8A5";
    private const string FolderGlyph = "\uE8B7";

    private readonly AdaptiveListInputElement _element;
    private readonly AdaptiveFilePathListInputElement? _pathElement;
    private readonly List<AdaptiveListItem> _items;
    private readonly Regex? _itemValidationRegex;

    private readonly string? _unreadableValue;

    private TextBox? _newItemTextBox;
    private Button? _addButton;
    private bool _wasEdited;

    public AdaptiveListInputControl(AdaptiveListInputElement element)
        : base(element)
    {
        _element = element;
        _pathElement = element as AdaptiveFilePathListInputElement;
        if (AdaptiveListValueCodec.TryParseItems(element.Value, out var parsedItems))
        {
            _items = parsedItems.Select(static item => new AdaptiveListItem(item)).ToList();
        }
        else
        {
            // Keep the value we could not read so that saving the form does not discard it.
            _items = [];
            _unreadableValue = element.Value;
            Logger.LogWarning($"Could not read the value of {element.ElementTypeString} '{element.Id}'.");
        }

        _itemValidationRegex = AdaptiveInputValidation.CreateRegex(element.ItemValidationPattern);

        RootPanel.Children.Add(_pathElement is null ? CreateStringAddControl() : CreatePathAddControl());
        CompleteLayout();
        RefreshItems();
    }

    public override string CurrentValue =>
        _unreadableValue is not null && !_wasEdited
            ? _unreadableValue
            : AdaptiveListValueCodec.ToItemsValue(_items.Select(static item => item.Source));

    public override void FocusInput()
    {
        (_newItemTextBox as Control ?? _addButton)?.Focus(FocusState.Programmatic);
    }

    private UIElement CreateStringAddControl()
    {
        var panel = new Grid { ColumnSpacing = 8 };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _newItemTextBox = new TextBox
        {
            PlaceholderText = string.IsNullOrEmpty(_element.Placeholder)
                ? RS_.GetString("AdaptiveStringListInput_Placeholder")
                : _element.Placeholder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _newItemTextBox.KeyDown += NewItemTextBox_KeyDown;

        _addButton = CreateTextButton(RS_.GetString("AdaptiveListInput_Add"), AddGlyph);
        _addButton.Click += (_, _) => AddTextItem();

        panel.Children.Add(_newItemTextBox);
        Grid.SetColumn(_addButton, 1);
        panel.Children.Add(_addButton);
        return panel;
    }

    private UIElement CreatePathAddControl()
    {
        var allowFiles = _pathElement!.AllowFiles;
        var allowFolders = _pathElement.AllowFolders;

        _addButton = CreateTextButton(
            allowFiles && allowFolders
                ? RS_.GetString("AdaptiveListInput_Add")
                : allowFiles
                    ? RS_.GetString("AdaptiveFilePathListInput_AddFile")
                    : RS_.GetString("AdaptiveFilePathListInput_AddFolder"),
            AddGlyph);
        _addButton.HorizontalAlignment = HorizontalAlignment.Left;

        if (allowFiles && allowFolders)
        {
            var flyout = new MenuFlyout();
            var addFile = new MenuFlyoutItem
            {
                Text = RS_.GetString("AdaptiveFilePathListInput_AddFile"),
                Icon = new FontIcon { Glyph = FileGlyph },
            };
            addFile.Click += async (_, _) => await PickFileAsync();
            flyout.Items.Add(addFile);

            var addFolder = new MenuFlyoutItem
            {
                Text = RS_.GetString("AdaptiveFilePathListInput_AddFolder"),
                Icon = new FontIcon { Glyph = FolderGlyph },
            };
            addFolder.Click += async (_, _) => await PickFolderAsync();
            flyout.Items.Add(addFolder);
            _addButton.Flyout = flyout;
        }
        else if (allowFiles)
        {
            _addButton.Click += async (_, _) => await PickFileAsync();
        }
        else
        {
            _addButton.Click += async (_, _) => await PickFolderAsync();
        }

        return _addButton;
    }

    private void NewItemTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            AddTextItem();
        }
    }

    private void AddTextItem()
    {
        if (_newItemTextBox is null || string.IsNullOrWhiteSpace(_newItemTextBox.Text))
        {
            return;
        }

        if (!IsItemValid(_newItemTextBox.Text))
        {
            ShowValidationError(GetItemValidationErrorMessage());
            _newItemTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (_element.PreventDuplicates &&
            _items.Any(item => DuplicateComparer.Equals(item.Value, _newItemTextBox.Text)))
        {
            ShowValidationError(GetDuplicateItemErrorMessage());
            _newItemTextBox.Focus(FocusState.Programmatic);
            return;
        }

        _items.Add(new AdaptiveListItem(_newItemTextBox.Text));
        _wasEdited = true;
        _newItemTextBox.Text = string.Empty;
        RefreshItems();
        UpdateValidationIfRequested();
        _newItemTextBox.Focus(FocusState.Programmatic);
    }

    private void AddPath(string? path, AdaptivePathItemKind kind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (_element.PreventDuplicates &&
            _items.Any(item => StringComparer.OrdinalIgnoreCase.Equals(item.Value, path)))
        {
            ShowValidationError(GetDuplicateItemErrorMessage());
            _addButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (!IsItemValid(path))
        {
            ShowValidationError(GetItemValidationErrorMessage());
            return;
        }

        _items.Add(new AdaptiveListItem(path, kind: kind));
        _wasEdited = true;
        RefreshItems();
        UpdateValidationIfRequested();
    }

    private async Task PickFileAsync()
    {
        try
        {
            var path = await AdaptiveFilePicker.PickFileAsync(
                this,
                _pathElement!.FileTypeFilter,
                RS_.GetString("AdaptiveFilePathListInput_AddFile"));
            AddPath(path, AdaptivePathItemKind.File);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick a file for an adaptive-card path list", ex);
        }
    }

    private async Task PickFolderAsync()
    {
        try
        {
            var path = await AdaptiveFilePicker.PickFolderAsync(
                this,
                RS_.GetString("AdaptiveFilePathListInput_AddFolder"));
            AddPath(path, AdaptivePathItemKind.Folder);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick a folder for an adaptive-card path list", ex);
        }
    }

    private void RefreshItems()
    {
        RefreshListItems(_items, CreateItemRow);
    }

    private UIElement CreateItemRow(AdaptiveListItem item)
    {
        var row = new Grid
        {
            ColumnSpacing = 8,
            MinHeight = ListItemMinHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var textColumn = 0;
        if (_pathElement is not null)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            var icon = new FontIcon
            {
                Glyph = IsFolderPath(item) ? FolderGlyph : FileGlyph,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
            row.Children.Add(icon);
            textColumn = 1;
        }

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock
        {
            Text = item.Value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(text, item.Value);
        Grid.SetColumn(text, textColumn);
        row.Children.Add(text);

        var removeButton = new Button
        {
            Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
            Content = new FontIcon { Glyph = DeleteGlyph, FontSize = 14 },
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var removeLabel = GetRemoveItemLabel(item.Value);
        AutomationProperties.SetName(removeButton, removeLabel);
        ToolTipService.SetToolTip(removeButton, removeLabel);
        removeButton.Click += (_, _) =>
        {
            _items.Remove(item);
            _wasEdited = true;
            RefreshItems();
            UpdateValidationIfRequested();
        };

        Grid.SetColumn(removeButton, textColumn + 1);
        row.Children.Add(removeButton);
        return row;
    }

    private bool IsFolderPath(AdaptiveListItem item)
    {
        if (_pathElement?.AllowFolders == true && _pathElement.AllowFiles == false)
        {
            return true;
        }

        if (_pathElement?.AllowFiles == true && _pathElement.AllowFolders == false)
        {
            return false;
        }

        if (item.PathKind is AdaptivePathItemKind.Folder)
        {
            return true;
        }

        if (item.PathKind is AdaptivePathItemKind.File)
        {
            return false;
        }

        return item.Value.EndsWith(Path.DirectorySeparatorChar) ||
            item.Value.EndsWith(Path.AltDirectorySeparatorChar) ||
            !Path.HasExtension(item.Value);
    }

    protected override bool UpdateValidation()
    {
        if (_element.IsRequired && _items.Count == 0)
        {
            ShowValidationError(string.IsNullOrEmpty(_element.ErrorMessage)
                ? RS_.GetString("AdaptiveListInput_RequiredError")
                : _element.ErrorMessage);
            return false;
        }

        if (_items.Any(item => !IsItemValid(item.Value)))
        {
            ShowValidationError(GetItemValidationErrorMessage());
            return false;
        }

        if (_element.PreventDuplicates && HasDuplicateItems())
        {
            ShowValidationError(GetDuplicateItemErrorMessage());
            return false;
        }

        ValidationError.Visibility = Visibility.Collapsed;
        return true;
    }

    private string GetItemValidationErrorMessage() =>
        string.IsNullOrEmpty(_element.ItemValidationErrorMessage)
            ? RS_.GetString("AdaptiveListInput_ItemValidationError")
            : _element.ItemValidationErrorMessage;

    private string GetDuplicateItemErrorMessage() =>
        string.IsNullOrEmpty(_element.DuplicateItemErrorMessage)
            ? RS_.GetString("AdaptiveListInput_DuplicateItemError")
            : _element.DuplicateItemErrorMessage;

    private StringComparer DuplicateComparer =>
        _pathElement is null ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private bool HasDuplicateItems()
    {
        var seen = new HashSet<string>(DuplicateComparer);
        return _items.Any(item => !seen.Add(item.Value));
    }

    private bool IsItemValid(string item) =>
        AdaptiveInputValidation.IsMatch(_itemValidationRegex, item);

    private sealed class AdaptiveListItem(AdaptiveListItemValue source, AdaptivePathItemKind? kind = null)
    {
        public AdaptiveListItem(string value, AdaptivePathItemKind? kind = null)
            : this(new AdaptiveListItemValue(value), kind)
        {
        }

        public AdaptiveListItemValue Source { get; } = source;

        public string Value => Source.Value;

        public AdaptivePathItemKind? PathKind { get; } = kind;
    }

    private enum AdaptivePathItemKind
    {
        File,
        Folder,
    }
}
