// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackLogItem : FallbackCommandItem
{
    private readonly CmdPalLogger logger;
    private readonly LogMessagesPage _logMessagesPage;

    private const string _id = "com.microsoft.cmdpal.log";

    public FallbackLogItem(CmdPalLogger logger)
        : base(new LogMessagesPage() { Id = _id }, Resources.builtin_log_subtitle, _id)
    {
        this.logger = logger;
        _logMessagesPage = (LogMessagesPage)Command!;
        Title = string.Empty;
        _logMessagesPage.Name = string.Empty;
        Subtitle = Properties.Resources.builtin_log_subtitle;

        var logPath = logger.CurrentVersionLogDirectoryPath;
        var openLogCommand = new OpenFileCommand(logPath) { Name = Resources.builtin_log_folder_command_name };
        MoreCommands = [new CommandContextItem(openLogCommand)];
    }

    public override void UpdateQuery(string query)
    {
        _logMessagesPage.Name = query.StartsWith('l') ? Properties.Resources.builtin_log_title : string.Empty;
        Title = _logMessagesPage.Name;
    }
}
