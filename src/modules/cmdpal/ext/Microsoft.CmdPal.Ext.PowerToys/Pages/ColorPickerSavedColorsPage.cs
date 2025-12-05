// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using ColorPicker.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class ColorPickerSavedColorsPage : DynamicListPage
{
    private readonly CommandItem _emptyContent;

    public ColorPickerSavedColorsPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\ColorPicker.png");
        Title = "Saved colors";
        Name = "ColorPickerSavedColors";
        Id = "com.microsoft.powertoys.colorpicker.savedColors";

        _emptyContent = new CommandItem()
        {
            Title = "No saved colors",
            Subtitle = "Pick a color first, then try again.",
            Icon = IconHelpers.FromRelativePath("Assets\\ColorPicker.png"),
        };

        EmptyContent = _emptyContent;
    }

    public override IListItem[] GetItems()
    {
        var result = ColorPickerService.Instance.GetSavedColorsAsync().GetAwaiter().GetResult();
        if (!result.Success || result.Value is null || result.Value.Count == 0)
        {
            return Array.Empty<IListItem>();
        }

        var search = SearchText;
        var filtered = string.IsNullOrWhiteSpace(search)
            ? result.Value
            : result.Value.Where(saved =>
                   saved.Hex.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   saved.Formats.Any(f => f.Value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                          f.Format.Contains(search, StringComparison.OrdinalIgnoreCase)));

        var items = filtered.Select(saved =>
        {
            var copyValue = SelectPreferredFormat(saved);
            var subtitle = BuildSubtitle(saved);

            var command = new CopySavedColorCommand(saved, copyValue);
            return (IListItem)new ListItem(new CommandItem(command))
            {
                Title = saved.Hex,
                Subtitle = subtitle,
                Icon = ColorSwatchIconFactory.Create(saved.R, saved.G, saved.B, saved.A),
            };
        }).ToArray();

        return items;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyContent.Subtitle = string.IsNullOrWhiteSpace(newSearch)
            ? "Pick a color first, then try again."
            : $"No saved colors matching '{newSearch}'";

        RaiseItemsChanged(0);
    }

    private static string SelectPreferredFormat(SavedColor saved) => saved.Hex;

    private static string BuildSubtitle(SavedColor saved)
    {
        var sb = new StringBuilder();
        foreach (var format in saved.Formats.Take(3))
        {
            if (sb.Length > 0)
            {
                sb.Append(" · ");
            }

            sb.Append(format.Value);
        }

        return sb.Length > 0 ? sb.ToString() : saved.Hex;
    }
}
