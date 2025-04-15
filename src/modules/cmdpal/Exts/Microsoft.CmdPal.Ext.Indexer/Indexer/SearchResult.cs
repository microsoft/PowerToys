// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal sealed class SearchResult
{
    public string ItemDisplayName { get; init; }

    public string ItemUrl { get; init; }

    public string LaunchUri { get; init; }

    public bool IsFolder { get; init; }

    public SearchResult(string name, string url, string filePath, bool isFolder)
    {
        ItemDisplayName = name;
        ItemUrl = url;
        IsFolder = isFolder;

        if (LaunchUri == null || LaunchUri.Length == 0)
        {
            // Launch the file with the default app, so use the file path
            LaunchUri = filePath;
        }
    }

    public static unsafe SearchResult Create(IPropertyStore propStore)
    {
        try
        {
            var key = NativeHelpers.PropertyKeys.PKEYItemNameDisplay;
            propStore.GetValue(&key, out var itemNameDisplay);

            key = NativeHelpers.PropertyKeys.PKEYItemUrl;
            propStore.GetValue(&key, out var itemUrl);

            key = NativeHelpers.PropertyKeys.PKEYKindText;
            propStore.GetValue(&key, out var kindText);

            var filePath = GetFilePath(ref itemUrl);
            var isFolder = IsFoder(ref kindText);

            // Create the actual result object
            var searchResult = new SearchResult(
                GetStringFromPropVariant(ref itemNameDisplay),
                GetStringFromPropVariant(ref itemUrl),
                filePath,
                isFolder);

            return searchResult;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to get property values from propStore.", ex);
            return null;
        }
    }

    private static bool IsFoder(ref PROPVARIANT kindText)
    {
        var kindString = GetStringFromPropVariant(ref kindText);
        return string.Equals(kindString, "Folder", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFilePath(ref PROPVARIANT itemUrl)
    {
        var filePath = GetStringFromPropVariant(ref itemUrl);
        filePath = UrlToFilePathConverter.Convert(filePath);
        return filePath;
    }

    private static string GetStringFromPropVariant(ref PROPVARIANT propVariant)
    {
        if (propVariant.Anonymous.Anonymous.vt == VARENUM.VT_LPWSTR)
        {
            var pwszVal = propVariant.Anonymous.Anonymous.Anonymous.pwszVal;
            if (pwszVal != null)
            {
                return pwszVal.ToString();
            }
        }

        return string.Empty;
    }
}
