// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit.Properties;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CopyPathCommand : InvokableCommand
{
    internal static IconInfo CopyPath { get; } = new("\uE8c8"); // Copy

    private static readonly CompositeFormat CopyFailedFormat = CompositeFormat.Parse(Resources.copy_failed);

    private readonly string _path;

    public CommandResult Result { get; set; } = CommandResult.ShowToast(Resources.CopyPathTextCommand_Result);

    public CopyPathCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.CopyPathTextCommand_Name;
        this.Icon = CopyPath;
    }

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(_path);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage("Copy failed: " + ex.Message) { State = MessageState.Error });
            return CommandResult.ShowToast(
            new ToastArgs
            {
                Message = string.Format(CultureInfo.CurrentCulture, CopyFailedFormat, ex.Message),
                Result = CommandResult.KeepOpen(),
            });
        }

        return Result;
    }
}
