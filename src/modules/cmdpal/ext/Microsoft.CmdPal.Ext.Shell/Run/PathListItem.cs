// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Run;

internal sealed partial class PathListItem : FileItem
{
    // FileItem takes care of our Icon for us.
    private readonly string path;

    internal override bool? IsDirectory => (bool)(base.IsDirectory!);

    public PathListItem(string path, string originalDir, Action<string>? addToHistory, ITelemetryService? telemetryService = null)
        : base(fullPath: path, isDirectory: Directory.Exists(path))
    {
        // RunDlg doesn't actually use the commands, we only ever use the TextToSuggest
        this.Command = new NoOpCommand();

        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        }

#if false // The way RunDialog works with TextToSuggest is just plain different than CmdPal
        if (isDirectory)
        {
            if (!path.EndsWith('\\'))
            {
                path += "\\";
            }

            if (!fileName.EndsWith('\\'))
            {
                fileName += "\\";
            }
        }
#endif

        this.path = path;

        Title = fileName; // Just the name of the file is the Title
        Subtitle = path; // What the user typed is the subtitle

        TextToSuggest = path;

#if false // Don't need all these in Run Rejuv
        MoreCommands = [
            new CommandContextItem(new OpenWithCommand(path)),
            new CommandContextItem(new ShowFileInFolderCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.E) },
            new CommandContextItem(new CopyPathCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.C) },
            new CommandContextItem(new OpenInConsoleCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.R) },
            new CommandContextItem(new OpenPropertiesCommand(path)),
         ];
#endif

    }
}
