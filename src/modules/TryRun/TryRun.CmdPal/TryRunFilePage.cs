// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.CmdPal;

internal sealed partial class TryRunFilePage : DynamicListPage
{
    private readonly string application;

    public TryRunFilePage(string application)
    {
        this.application = application;
        Id = "com.microsoft.powertoys.tryrun.file";
        Name = "Configure";
        Title = "Try Run a file";
        PlaceholderText = "Paste a full file or folder path";
        Icon = new IconInfo("\uE768");
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        try
        {
            var selection = CmdPalLaunch.ParseQuery(SearchText);
            return
            [
                new ListItem(new OpenTryRunCommand(application, selection))
                {
                    Title = selection.Length == 0 ? "Open Try Run" : "Configure this selection in Try Run",
                    Subtitle = selection.Length == 0 ? "Select your program and permissions before running" : selection[0],
                },
            ];
        }
        catch (ArgumentException)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Enter one full local file or folder path", Subtitle = "Paths only; commands and command-line arguments are not accepted here" }];
        }
    }
}
