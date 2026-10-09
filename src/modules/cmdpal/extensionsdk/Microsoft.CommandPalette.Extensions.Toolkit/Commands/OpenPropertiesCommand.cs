// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCsWin32;
using Microsoft.CommandPalette.Extensions.Toolkit.Properties;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class OpenPropertiesCommand : InvokableCommand
{
    internal static IconInfo OpenPropertiesIcon { get; } = new("\uE90F");

    private readonly string _path;

    private static unsafe bool ShowFileProperties(string filename)
    {
        var propertiesPtr = Marshal.StringToHGlobalUni("properties");
        var filenamePtr = Marshal.StringToHGlobalUni(filename);

        try
        {
            var info = new Shell32.SHELLEXECUTEINFOW
            {
                CbSize = (uint)sizeof(Shell32.SHELLEXECUTEINFOW),
                LpVerb = propertiesPtr,
                LpFile = filenamePtr,
                Show = (int)SHOW_WINDOW_CMD.SW_SHOW,
                FMask = global::Windows.Win32.PInvoke.SEE_MASK_INVOKEIDLIST,
            };

            return Shell32.ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(propertiesPtr);
        }
    }

    public OpenPropertiesCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.OpenPropertiesCommand_Name;
        this.Icon = OpenPropertiesIcon;
    }

    public override CommandResult Invoke()
    {
        try
        {
            ShowFileProperties(_path);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage($"Error showing file properties '{_path}'\n{ex.Message}\n{ex.StackTrace}") { State = MessageState.Error });
        }

        return CommandResult.Dismiss();
    }
}
