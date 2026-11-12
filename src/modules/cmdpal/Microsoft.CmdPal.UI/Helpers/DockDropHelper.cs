// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class DockDropHelper
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";
    private const uint SigdnNormalDisplay = 0;

    public static Task<(string Name, string Target)> GetBookmarkAsync(string path, CancellationToken cancellationToken)
    {
        return GetBookmarkAsync(path, GetAppsFolderDisplayName, cancellationToken);
    }

    internal static Task<(string Name, string Target)> GetBookmarkAsync(string path, Func<string, string?> getAppDisplayName, CancellationToken cancellationToken)
    {
        var lookup = Task.Run(() => GetBookmark(path, getAppDisplayName), cancellationToken);

        // Observe failures even if the caller stops waiting for the Shell lookup.
        _ = lookup.ContinueWith(
            static task =>
            {
                _ = task.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return lookup.WaitAsync(cancellationToken);
    }

    internal static (string Name, string Target) GetBookmark(string path, Func<string, string?> getAppDisplayName)
    {
        // Dropped AppsFolder items can expose an AUMID instead of a filesystem path.
        if (!Path.IsPathRooted(path))
        {
            var appUserModelId = path.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
                ? path[AppsFolderPrefix.Length..]
                : path;
            var displayName = getAppDisplayName(appUserModelId);
            if (displayName is not null)
            {
                var name = string.IsNullOrWhiteSpace(displayName) ? appUserModelId : displayName;
                return (name, AppsFolderPrefix + appUserModelId);
            }
        }

        return (Path.GetFileNameWithoutExtension(path), path);
    }

    private static string? GetAppsFolderDisplayName(string appUserModelId)
    {
        nint itemIdList = 0;
        nint displayName = 0;
        try
        {
            // AppsFolder resolves both packaged apps and registered desktop apps.
            var hr = NativeMethods.SHParseDisplayName(AppsFolderPrefix + appUserModelId, 0, out itemIdList, 0, 0);
            if (hr < 0 || itemIdList == 0)
            {
                return null;
            }

            hr = NativeMethods.SHGetNameFromIDList(itemIdList, SigdnNormalDisplay, out displayName);
            return hr >= 0 ? Marshal.PtrToStringUni(displayName) ?? string.Empty : string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(displayName);
            Marshal.FreeCoTaskMem(itemIdList);
        }
    }

    // Local, because unit tests, meh
    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHParseDisplayName(string name, nint bindContext, out nint itemIdList, uint requestedAttributes, nint attributes);

        [LibraryImport("shell32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHGetNameFromIDList(nint itemIdList, uint nameFormat, out nint displayName);
    }
}
