// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit.Properties;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class OpenFileCommand : InvokableCommand
{
    internal static IconInfo OpenFile { get; } = new("\uE8E5"); // OpenFile

    private readonly string _fullPath;

    public CommandResult Result { get; set; } = CommandResult.Dismiss();

    public OpenFileCommand(string fullPath)
    {
        this._fullPath = fullPath;
        this.Name = Resources.OpenFileCommand_Name;
        this.Icon = OpenFile;
    }

    public override CommandResult Invoke()
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = _fullPath;
            process.StartInfo.UseShellExecute = true;

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                ExtensionHost.LogMessage($"Unable to open {_fullPath}\n{ex}");
            }
        }

        return Result;
    }
}
