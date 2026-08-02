// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal static class AdaptiveFilePicker
{
    public static async Task<string?> PickFileAsync(
        FrameworkElement owner,
        IEnumerable<string> fileTypeFilter,
        string commitButtonText)
    {
        var picker = new FileOpenPicker(GetOwnerWindowId(owner))
        {
            CommitButtonText = commitButtonText,
        };

        var filterWasAdded = false;
        foreach (var filter in fileTypeFilter.Select(NormalizeFileTypeFilter).Where(static filter => filter is not null))
        {
            picker.FileTypeFilter.Add(filter!);
            filterWasAdded = true;
        }

        if (!filterWasAdded)
        {
            picker.FileTypeFilter.Add("*");
        }

        return (await picker.PickSingleFileAsync())?.Path;
    }

    public static async Task<string?> PickFolderAsync(FrameworkElement owner, string commitButtonText)
    {
        var picker = new FolderPicker(GetOwnerWindowId(owner))
        {
            CommitButtonText = commitButtonText,
        };
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    private static WindowId GetOwnerWindowId(FrameworkElement owner)
    {
        var contentIslandEnvironment = owner.XamlRoot?.ContentIslandEnvironment;
        if (contentIslandEnvironment is null)
        {
            throw new InvalidOperationException("The adaptive-card input is not attached to a window.");
        }

        return contentIslandEnvironment.AppWindowId;
    }

    private static string? NormalizeFileTypeFilter(string filter)
    {
        var normalized = filter.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized is "*" or "*.*")
        {
            return "*";
        }

        if (normalized.StartsWith("*.", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        return normalized.StartsWith('.')
            ? normalized
            : $".{normalized}";
    }
}
