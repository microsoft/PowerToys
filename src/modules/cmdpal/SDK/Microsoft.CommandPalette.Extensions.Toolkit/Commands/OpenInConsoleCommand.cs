// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit.Properties;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class OpenInConsoleCommand : InvokableCommand
{
    internal static IconInfo OpenInConsoleIcon { get; } = new("\uE756"); // "CommandPrompt"

    private readonly string _path;
    private bool _isDirectory;

    public OpenInConsoleCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.OpenInConsoleCommand_Name;
        this.Icon = OpenInConsoleIcon;
    }

    public static OpenInConsoleCommand FromDirectory(string directory) => new(directory) { _isDirectory = true };

    public static OpenInConsoleCommand FromFile(string file) => new(file);

    public override CommandResult Invoke()
    {
        using (var process = new Process())
        {
            process.StartInfo.WorkingDirectory = _isDirectory ? _path : Path.GetDirectoryName(_path);
            process.StartInfo.FileName = "cmd.exe";

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                ExtensionHost.LogMessage(new LogMessage($"Unable to open '{_path}'\n{ex.Message}\n{ex.StackTrace}") { State = MessageState.Error });
            }
        }

        return CommandResult.Dismiss();
    }
}
