// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public class ShellLinkHelper : IShellLinkHelper
{
    // Contains the description of the app
    public string Description { get; set; } = string.Empty;

    // Contains the arguments to the app
    public string Arguments { get; set; } = string.Empty;

    public bool HasArguments { get; set; }

    // Retrieve the target path using Shell Link
    public unsafe string RetrieveTargetPath(string path)
    {
        var target = string.Empty;

        const int MAX_PATH = 260;
        IShellLinkW* link = null;
        try
        {
            PInvoke.CoCreateInstance(typeof(ShellLink).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out link).ThrowOnFailure();

            const int STGM_READ = 0;

            IPersistFile* persistFile = null;
            Guid iid = typeof(IPersistFile).GUID;
            ((IUnknown*)link)->QueryInterface(&iid, (void**)&persistFile);
            if (persistFile != null)
            {
                try
                {
                    persistFile->Load(path, STGM_READ);
                }
                catch (System.IO.FileNotFoundException)
                {
                    // Log.Exception($"Failed to load {path}, {e.Message}", e, GetType());
                    return string.Empty;
                }
                finally
                {
                    persistFile->Release();
                }
            }

            var hwnd = HWND.Null;
            const uint SLR_NO_UI = 0x1;
            link->Resolve(hwnd, SLR_NO_UI);

            PWSTR pBuffer = null;
            link->GetPath(pBuffer, MAX_PATH, null, 0x1);

            target = pBuffer.ToString();

            // To set the app description
            if (!string.IsNullOrEmpty(target))
            {
                PWSTR pszName = null;
                try
                {
                    link->GetDescription(pszName, MAX_PATH).ThrowOnFailure();
                    Description = pszName.ToString();
                }
                catch (Exception)
                {
                    // Log.Exception($"Failed to fetch description for {target}, {e.Message}", e, GetType());
                    Description = string.Empty;
                }

                PWSTR pszArgs = null;
                link->GetArguments(pszArgs, MAX_PATH);

                Arguments = pszArgs.ToString();

                // Set variable to true if the program takes in any arguments
                if (Arguments.Length != 0)
                {
                    HasArguments = true;
                }
            }
        }
        finally
        {
            link->Release();
        }

        return target;
    }
}
