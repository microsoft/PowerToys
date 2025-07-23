// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CopyPathCommand : InvokableCommand
{
    internal static IconInfo CopyPath { get; } = new("\uE8c8"); // Copy

    private readonly string _path;

    public CommandResult Result { get; set; } = CommandResult.KeepOpen();

    public CopyPathCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = "Copy path";
        this.Icon = CopyPath;
    }

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(_path);
        }
        catch
        {
        }

        return Result;
    }
}
