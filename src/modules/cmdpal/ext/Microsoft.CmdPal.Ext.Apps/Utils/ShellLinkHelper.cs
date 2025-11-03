// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        PInvoke.CoCreateInstance(typeof(ShellLink).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out link).ThrowOnFailure();
        using var linkHandle = new SafeComHandle((IntPtr)link);

        const int STGMREAD = 0;

        IPersistFile* persistFile = null;
        Guid iid = typeof(IPersistFile).GUID;
        ((IUnknown*)link)->QueryInterface(&iid, (void**)&persistFile);
        if (persistFile is not null)
        {
            using var persistFileHandle = new SafeComHandle((IntPtr)persistFile);
            try
            {
                persistFile->Load(path, STGMREAD);
            }
            catch (System.IO.FileNotFoundException)
            {
                // Log.Exception($"Failed to load {path}, {e.Message}", e, GetType());
                return string.Empty;
            }
        }

        var hwnd = HWND.Null;
        const uint SLR_NO_UI = 0x1;
        link->Resolve(hwnd, SLR_NO_UI);

        var buffer = stackalloc char[MAX_PATH];

        var hr = link->GetPath((PWSTR)buffer, MAX_PATH, null, 0x1);

        target = hr.Succeeded ? new string(buffer) : string.Empty;

        // To set the app description
        if (!string.IsNullOrEmpty(target))
        {
            var descBuffer = stackalloc char[MAX_PATH];
            var desHr = link->GetDescription(descBuffer, MAX_PATH);
            Description = desHr.Succeeded ? new string(descBuffer) : string.Empty;

            var argsBuffer = stackalloc char[MAX_PATH];
            var argHr = link->GetArguments(argsBuffer, MAX_PATH);

            Arguments = argHr.Succeeded ? new string(argsBuffer) : string.Empty;

            // Set variable to true if the program takes in any arguments
            if (Arguments.Length != 0)
            {
                HasArguments = true;
            }
        }

        return target;
    }
}
