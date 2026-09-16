// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Resources;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.TryRun.Launching;

namespace Microsoft.CmdPal.Common.Commands;

public sealed partial class OpenInTryRunCommand : InvokableCommand
{
    private static readonly ResourceManager Strings = new("Microsoft.CmdPal.Common.Commands.TryRun", typeof(OpenInTryRunCommand).Assembly);
    private readonly string _path;
    private readonly string _arguments;

    public OpenInTryRunCommand(string path, string arguments = "")
    {
        _path = path;
        _arguments = arguments;
        Id = "com.microsoft.powertoys.tryrun.selection";
        Name = Strings.GetString("OpenInTryRun", CultureInfo.CurrentUICulture)!;
        Icon = new IconInfo("\uE72E");
    }

    public static CommandContextItem? ForFile(string path) => CmdPalHandoff.IsLocalPath(path) ? new(new OpenInTryRunCommand(path)) : null;

    public static CommandContextItem? ForApplication(string path, string arguments) =>
        CmdPalHandoff.SupportsApplication(path) ? new(new OpenInTryRunCommand(path, arguments)) : null;

    public override ICommandResult Invoke()
    {
        try
        {
            var selection = CmdPalHandoff.CreateSelection(_path, _arguments);
            var application = CmdPalHandoff.FindApplication(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_APP"));
            CmdPalHandoff.OpenAsync(application, selection, CancellationToken.None).GetAwaiter().GetResult();
            return CommandResult.Dismiss();
        }
        catch (Exception exception)
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = Strings.GetString("CouldNotOpen", CultureInfo.CurrentUICulture) + " " + exception.Message,
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
