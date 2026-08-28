// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Input;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentLinkViewModel(ILinkContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<ILinkContent>(model, context)
{
    public string Text { get; private set; } = string.Empty;

    public Uri? Link { get; private set; }

    public bool IsLink => Link is not null;

    public bool IsText => !IsLink;

    [RelayCommand]
    private void Navigate()
    {
        if (Link is { } link)
        {
            ShellHelpers.OpenInShell(link.ToString());
        }
    }

    protected override void ReadProperties()
    {
        Link = Model.Link;
        Text = Model.Text ?? string.Empty;
        if (Text.Length == 0 && Link is not null)
        {
            Text = Link.ToString();
        }

        UpdateProperty(nameof(Text), nameof(Link), nameof(IsLink), nameof(IsText));
    }
}
