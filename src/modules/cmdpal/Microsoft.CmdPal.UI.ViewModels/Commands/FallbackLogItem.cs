// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackLogItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.log";

    private readonly OpenFileCommand _openLogCommand;

    public FallbackLogItem(IApplicationInfoService applicationInfoService)
        : base(new OpenFileCommand(applicationInfoService.LogDirectory) { Id = _id }, Resources.builtin_log_folder_command_name, _id)
    {
        _openLogCommand = (OpenFileCommand)Command!;
        Title = string.Empty;
        _openLogCommand.Name = string.Empty;
        Subtitle = Resources.builtin_log_folder_command_name;

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
            Icon = _openLogCommand.Icon,
        };
        MoreCommands = [new CommandContextItem(openLogFileCommand)];
    }

    public override void UpdateQuery(string query)
    {
        _openLogCommand.Name = query.StartsWith("l", StringComparison.OrdinalIgnoreCase) ? Resources.builtin_log_folder_command_name : string.Empty;
        Title = _openLogCommand.Name;
    }
}
