// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCsWin32;
using Microsoft.CmdPal.Common.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Common.Commands;

public partial class OpenWithCommand : InvokableCommand
{
    internal static IconInfo OpenWithIcon { get; } = new("\uE7AC");

    private readonly string _path;

    private static unsafe bool OpenWith(string filename)
    {
        var filenamePtr = Marshal.StringToHGlobalUni(filename);
        var verbPtr = Marshal.StringToHGlobalUni("openas");

        try
        {
            var info = new Shell32.SHELLEXECUTEINFOW
            {
                CbSize = (uint)sizeof(Shell32.SHELLEXECUTEINFOW),
                LpVerb = verbPtr,
                LpFile = filenamePtr,
                Show = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL,
                FMask = global::Windows.Win32.PInvoke.SEE_MASK_INVOKEIDLIST,
            };

            return Shell32.ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(verbPtr);
        }
    }

    public OpenWithCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.Indexer_Command_OpenWith;
        this.Icon = OpenWithIcon;
    }

    public override CommandResult Invoke()
    {
        OpenWith(_path);

        return CommandResult.GoHome();
    }
}
