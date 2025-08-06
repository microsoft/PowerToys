// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

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

        if (LaunchUri is null || LaunchUri.Length == 0)
        {
            // Launch the file with the default app, so use the file path
            LaunchUri = filePath;
        }
    }

    public static unsafe SearchResult Create(IPropertyStore propStore)
    {
        try
        {
            propStore.GetValue(NativeHelpers.PropertyKeys.PKEYItemNameDisplay, out var itemNameDisplay);
            propStore.GetValue(NativeHelpers.PropertyKeys.PKEYItemUrl, out var itemUrl);
            propStore.GetValue(NativeHelpers.PropertyKeys.PKEYKindText, out var kindText);

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

    private static bool IsFoder(ref PropVariant kindText)
    {
        var kindString = GetStringFromPropVariant(ref kindText);
        return string.Equals(kindString, "Folder", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFilePath(ref PropVariant itemUrl)
    {
        var filePath = GetStringFromPropVariant(ref itemUrl);
        filePath = UrlToFilePathConverter.Convert(filePath);
        return filePath;
    }

    private static string GetStringFromPropVariant(ref PropVariant propVariant)
    {
        if (propVariant.VarType == System.Runtime.InteropServices.VarEnum.VT_LPWSTR)
        {
            var pwszVal = propVariant._ptr;

            if (pwszVal == IntPtr.Zero)
            {
                return string.Empty;
            }

            // convert to string
            var str = Marshal.PtrToStringUni(pwszVal);

            return str;
        }

        return string.Empty;
    }
}
