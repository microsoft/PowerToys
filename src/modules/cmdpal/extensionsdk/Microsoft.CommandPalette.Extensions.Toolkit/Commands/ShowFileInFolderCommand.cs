// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ShowFileInFolderCommand : InvokableCommand
{
    private readonly string _path;
    private static readonly IconInfo Ico = new("\uE838");

    public CommandResult Result { get; set; } = CommandResult.Dismiss();

    public ShowFileInFolderCommand(string path)
    {
        _path = path;
        Name = "Show in folder";
        Icon = Ico;
    }

    public override CommandResult Invoke()
    {
        if (Path.Exists(_path))
        {
            try
            {
                var argument = "/select, \"" + _path + "\"";
                Process.Start("explorer.exe", argument);
            }
            catch (Exception)
            {
            }
        }

        return Result;
    }
}
