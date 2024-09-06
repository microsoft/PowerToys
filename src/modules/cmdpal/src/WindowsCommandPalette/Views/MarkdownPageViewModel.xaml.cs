// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DeveloperCommandPalette;
using Microsoft.CmdPal.Extensions;

namespace WindowsCommandPalette.Views;

public sealed class MarkdownPageViewModel : PageViewModel
{
    public IMarkdownPage Page => (IMarkdownPage)PageAction;

    public string[] MarkdownContent { get; set; } = [string.Empty];

    public string Title => Page.Title;

    private IEnumerable<ICommandContextItem> GetCommandContextItems()
    {
        return Page.Commands.Where(i => i is ICommandContextItem).Select(i => (ICommandContextItem)i);
    }

    public bool HasMoreCommands => GetCommandContextItems().Any();

    public IList<ContextItemViewModel> ContextActions => GetCommandContextItems().Select(a => new ContextItemViewModel(a)).ToList();

    public MarkdownPageViewModel(IMarkdownPage page)
        : base(page)
    {
    }

    public async Task InitialRender(MarkdownPage markdownPage)
    {
        var t = new Task<string[]>(Page.Bodies);

        t.Start();
        MarkdownContent = await t;
    }
}
