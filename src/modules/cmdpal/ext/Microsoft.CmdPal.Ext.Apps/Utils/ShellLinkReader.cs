// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

internal static class ShellLinkReader
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    public static unsafe ShellLinkInfo? Read(string path)
    {
        const int MAX_PATH = 260;
        IShellLinkW* link = null;

        PInvoke.CoCreateInstance(typeof(ShellLink).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out link).ThrowOnFailure();
        using var linkHandle = new SafeComHandle((IntPtr)link);

        const int STGMREAD = 0;

        IPersistFile* persistFile = null;
        Guid iid = typeof(IPersistFile).GUID;
        var queryResult = ((IUnknown*)link)->QueryInterface(&iid, (void**)&persistFile);
        using var persistFileHandle = new SafeComHandle((IntPtr)persistFile);
        if (queryResult.Failed || persistFile is null || persistFile->Load(path, STGMREAD).Failed)
        {
            return null;
        }

        const uint SLR_NO_UI = 0x1;
        link->Resolve(HWND.Null, SLR_NO_UI);

        var buffer = stackalloc char[MAX_PATH];
        buffer[0] = '\0';
        var hr = link->GetPath((PWSTR)buffer, MAX_PATH, null, 0x1);
        var target = hr.Succeeded ? new string(buffer) : string.Empty;

        buffer[0] = '\0';
        var iconLocation = string.Empty;
        var iconIndex = 0;
        var iconResult = link->GetIconLocation(buffer, MAX_PATH, &iconIndex);
        if (iconResult.Succeeded && buffer[0] != '\0')
        {
            var iconPath = Environment.ExpandEnvironmentVariables(new string(buffer));
            iconLocation = FormattableString.Invariant($"{iconPath},{iconIndex}");
        }

        var packagedAppUserModelId = string.Empty;
        var description = string.Empty;
        var arguments = string.Empty;
        if (string.IsNullOrEmpty(target))
        {
            packagedAppUserModelId = GetPackagedAppUserModelId(
                RetrieveStringProperty(path, PInvoke.PKEY_Link_TargetParsingPath));
        }
        else
        {
            buffer[0] = '\0';
            var desHr = link->GetDescription(buffer, MAX_PATH);
            description = desHr.Succeeded ? new string(buffer) : string.Empty;

            buffer[0] = '\0';
            var argHr = link->GetArguments(buffer, MAX_PATH);
            arguments = argHr.Succeeded ? new string(buffer) : string.Empty;
        }

        buffer[0] = '\0';
        var directoryResult = link->GetWorkingDirectory(buffer, MAX_PATH);
        var workingDirectory = directoryResult.Succeeded ? new string(buffer) : string.Empty;
        IPropertyStore* linkPropertyStore = null;
        iid = typeof(IPropertyStore).GUID;
        var propertyStoreResult = ((IUnknown*)link)->QueryInterface(&iid, (void**)&linkPropertyStore);
        using var linkPropertyStoreHandle = new SafeComHandle((IntPtr)linkPropertyStore);
        var explicitAppUserModelId = propertyStoreResult.Succeeded && linkPropertyStore is not null
            ? RetrieveStringProperty(linkPropertyStore, PInvoke.PKEY_AppUserModel_ID)
            : RetrieveStringProperty(path, PInvoke.PKEY_AppUserModel_ID);

        return new ShellLinkInfo(target, description, arguments, workingDirectory, packagedAppUserModelId, iconLocation, explicitAppUserModelId);
    }

    private static unsafe string RetrieveStringProperty(string path, in PROPERTYKEY key)
    {
        var iid = typeof(IPropertyStore).GUID;
        var queryResult = PInvoke.SHGetPropertyStoreFromParsingName(
            path,
            null,
            GETPROPERTYSTOREFLAGS.GPS_DEFAULT,
            iid,
            out var propertyStoreObject);
        if (queryResult.Failed || propertyStoreObject is null)
        {
            return string.Empty;
        }

        var propertyStore = (IPropertyStore*)propertyStoreObject;
        using var propertyStoreHandle = new SafeComHandle((IntPtr)propertyStore);
        return RetrieveStringProperty(propertyStore, key);
    }

    private static unsafe string RetrieveStringProperty(IPropertyStore* propertyStore, in PROPERTYKEY key)
    {
        PROPVARIANT value = default;
        try
        {
            var getValueResult = propertyStore->GetValue(key, out value);
            if (getValueResult.Failed)
            {
                return string.Empty;
            }

            var conversionResult = PInvoke.PropVariantToStringAlloc(value, out var stringValue);
            if (conversionResult.Failed || stringValue.Value is null)
            {
                return string.Empty;
            }

            try
            {
                return stringValue.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(stringValue.Value);
            }
        }
        finally
        {
            _ = PInvoke.PropVariantClear(ref value);
        }
    }

    private static string GetPackagedAppUserModelId(string targetParsingPath)
    {
        var candidate = targetParsingPath.Trim();
        if (candidate.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[AppsFolderPrefix.Length..];
        }

        var appSeparator = candidate.IndexOf('!');
        return appSeparator > 0
            && appSeparator < candidate.Length - 1
            && candidate.AsSpan(0, appSeparator).Contains('_')
                ? candidate
                : string.Empty;
    }
}
