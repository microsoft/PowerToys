// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:Variable names should begin with lower-case letter", Justification = "This file has more than a couple Windows constants in it, which don't make sense to rename")]
public class Utilities
{
    /// <summary>
    /// Used to produce a path to a settings folder which your app can use.
    /// If your app is running packaged, this will return the redirected local
    /// app data path (Packages/{your_pfn}/LocalState). If not, it'll return
    /// %LOCALAPPDATA%\{settingsFolderName}.
    ///
    /// Does not ensure that the directory exists. Callers should call
    /// CreateDirectory before writing settings files to this directory.
    /// </summary>
    /// <example>
    /// var directory = Utilities.BaseSettingsPath("Some.Unique.String.Here");
    /// Directory.CreateDirectory(directory);
    /// </example>
    /// <param name="settingsFolderName">A fallback directory name to use
    /// inside of %LocalAppData%, in the case this app is not currently running
    /// in a package context</param>
    /// <returns>The path to a folder to use for storing settings.</returns>
    public static string BaseSettingsPath(string settingsFolderName)
    {
        // KF_FLAG_FORCE_APP_DATA_REDIRECTION, when engaged, causes SHGet... to return
        // the new AppModel paths (Packages/xxx/RoamingState, etc.) for standard path requests.
        // Using this flag allows us to avoid Windows.Storage.ApplicationData completely.
        var FOLDERID_LocalAppData = new Guid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");
        var hr = PInvoke.SHGetKnownFolderPath(
            FOLDERID_LocalAppData,
            (uint)KNOWN_FOLDER_FLAG.KF_FLAG_FORCE_APP_DATA_REDIRECTION,
            null,
            out var localAppDataFolder);

        if (hr.Succeeded)
        {
            var basePath = new string(localAppDataFolder.ToString());
            if (!IsPackaged())
            {
                basePath = Path.Combine(basePath, settingsFolderName);
            }

            return basePath;
        }
        else
        {
            throw Marshal.GetExceptionForHR(hr.Value)!;
        }
    }

    /// <summary>
    /// Can be used to quickly determine if this process is running with package identity.
    /// </summary>
    /// <returns>true iff the process is running with package identity</returns>
    public static bool IsPackaged()
    {
        uint bufferSize = 0;
        var bytes = Array.Empty<byte>();

        // CsWinRT apparently won't generate this constant
        var APPMODEL_ERROR_NO_PACKAGE = (WIN32_ERROR)15700;
        unsafe
        {
            fixed (byte* p = bytes)
            {
                // We don't actually need the package ID. We just need to know
                // if we have a package or not, and APPMODEL_ERROR_NO_PACKAGE
                // is a quick way to find out.
                var win32Error = PInvoke.GetCurrentPackageId(ref bufferSize, p);
                return win32Error != APPMODEL_ERROR_NO_PACKAGE;
            }
        }
    }
}
