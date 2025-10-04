// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Detects when text content is a valid existing file or directory path and exposes basic metadata.
/// </summary>
internal sealed class TextFileSystemMetadataProvider : IClipboardMetadataProvider
{
    public string SectionTitle => "File";

    public bool CanHandle(ClipboardItem item)
    {
        if (!item.IsText || string.IsNullOrWhiteSpace(item.Content))
        {
            return false;
        }

        var text = item.Content.Trim();

        return IsPotentialPath(text) && (File.Exists(text) || Directory.Exists(text));
    }

    public IEnumerable<DetailsElement> GetDetails(ClipboardItem item)
    {
        var result = new List<DetailsElement>();
        if (!item.IsText || string.IsNullOrWhiteSpace(item.Content))
        {
            return result;
        }

        var path = item.Content.Trim();
        try
        {
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                result.Add(new DetailsElement { Key = "Name", Data = new DetailsLink(fi.Name) });
                result.Add(new DetailsElement { Key = "Path", Data = new DetailsLink(UrlHelper.NormalizeUrl(fi.FullName), fi.FullName) });
                result.Add(new DetailsElement { Key = "Type", Data = new DetailsLink(fi.Extension) });
                result.Add(new DetailsElement { Key = "Size", Data = new DetailsLink(SizeFormatter.FormatSize(fi.Length)) });
                result.Add(new DetailsElement { Key = "Modified", Data = new DetailsLink(fi.LastWriteTime.ToString(CultureInfo.CurrentCulture)) });
                result.Add(new DetailsElement { Key = "Created", Data = new DetailsLink(fi.CreationTime.ToString(CultureInfo.CurrentCulture)) });
            }
            else if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                result.Add(new DetailsElement { Key = "Name", Data = new DetailsLink(di.Name) });
                result.Add(new DetailsElement { Key = "Path", Data = new DetailsLink(UrlHelper.NormalizeUrl(di.FullName), di.FullName) });
                result.Add(new DetailsElement { Key = "Type", Data = new DetailsLink("Folder") });
                result.Add(new DetailsElement { Key = "Modified", Data = new DetailsLink(di.LastWriteTime.ToString(CultureInfo.CurrentCulture)) });
                result.Add(new DetailsElement { Key = "Created", Data = new DetailsLink(di.CreationTime.ToString(CultureInfo.CurrentCulture)) });
            }
        }
        catch
        {
        }

        return result;
    }

    public IEnumerable<ProviderAction> GetActions(ClipboardItem item)
    {
        if (!item.IsText || string.IsNullOrWhiteSpace(item.Content))
        {
            yield break;
        }

        var path = item.Content.Trim();

        if (File.Exists(path))
        {
            // Open file
            var open = new CommandContextItem(new OpenFileCommand(path)) { RequestedShortcut = KeyChords.OpenUrl };
            yield return new ProviderAction(WellKnownActionIds.Open, open);

            // Show in folder (select)
            var show = new CommandContextItem(new ShowFileInFolderCommand(path)) { RequestedShortcut = WellKnownKeyChords.OpenFileLocation };
            yield return new ProviderAction(WellKnownActionIds.OpenLocation, show);

            // Copy path
            var copy = new CommandContextItem(new CopyPathCommand(path)) { RequestedShortcut = WellKnownKeyChords.CopyFilePath };
            yield return new ProviderAction(WellKnownActionIds.CopyPath, copy);

            // Open in console at file location
            var openConsole = new CommandContextItem(OpenInConsoleCommand.FromFile(path)) { RequestedShortcut = WellKnownKeyChords.OpenInConsole };
            yield return new ProviderAction(WellKnownActionIds.OpenConsole, openConsole);
        }
        else if (Directory.Exists(path))
        {
            // Open folder
            var openFolder = new CommandContextItem(new OpenFileCommand(path)) { RequestedShortcut = KeyChords.OpenUrl };
            yield return new ProviderAction(WellKnownActionIds.Open, openFolder);

            // Open in console
            var openConsole = new CommandContextItem(OpenInConsoleCommand.FromDirectory(path)) { RequestedShortcut = WellKnownKeyChords.OpenInConsole };
            yield return new ProviderAction(WellKnownActionIds.OpenConsole, openConsole);

            // Copy path
            var copy = new CommandContextItem(new CopyPathCommand(path)) { RequestedShortcut = WellKnownKeyChords.CopyFilePath };
            yield return new ProviderAction(WellKnownActionIds.CopyPath, copy);
        }
    }

    private static bool IsPotentialPath(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        return text.Length >= 2 && char.IsLetter(text[0]) && text[1] == ':';
    }
}
