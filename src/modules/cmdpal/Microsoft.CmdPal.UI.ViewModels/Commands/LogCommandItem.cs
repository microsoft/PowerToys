// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class LogCommandItem : CommandItem
{
    public LogCommandItem(IApplicationInfoService applicationInfoService)
        : base(new OpenFileCommand(applicationInfoService.LogDirectory)
        {
            Id = "com.microsoft.cmdpal.log",
            Name = Resources.builtin_log_folder_command_name,
        })
    {
        var openLogFileCommand = new AnonymousCommand(() =>
        {
            var logFile = applicationInfoService.LogFile;
            if (File.Exists(logFile))
            {
                new OpenFileCommand(logFile).Invoke();
            }
        })
        {
            Name = Resources.builtin_log_file_command_name,
            Icon = (IconInfo)Command!.Icon,
        };
        MoreCommands = [new CommandContextItem(openLogFileCommand)];
    }
}
