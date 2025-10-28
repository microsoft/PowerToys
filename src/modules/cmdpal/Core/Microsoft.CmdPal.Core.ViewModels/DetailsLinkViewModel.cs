// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class DetailsLinkViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    private static readonly string[] _initProperties = [
        nameof(Text),
        nameof(Link),
        nameof(IsLink),
        nameof(IsText),
        nameof(NavigateCommand)];

    private readonly ExtensionObject<IDetailsLink> _dataModel =
        new(_detailsElement.Data as IDetailsLink);

    public string Text { get; private set; } = string.Empty;

    public Uri? Link { get; private set; }

    public bool IsLink => Link is not null;

    public bool IsText => !IsLink;

    public RelayCommand? NavigateCommand { get; private set; }

    public override void InitializeProperties()
    {
        base.InitializeProperties();
        var model = _dataModel.Unsafe;
        if (model is null)
        {
            return;
        }

        Text = model.Text ?? string.Empty;
        Link = model.Link;
        if (string.IsNullOrEmpty(Text) && Link is not null)
        {
            Text = Link.ToString();
        }

        if (Link is not null)
        {
            // Custom command to open a link in the default browser or app,
            // depending on the link type.
            // Binding Link to a Hyperlink(Button).NavigateUri works only for
            // certain URI schemes (e.g., http, https) and cannot open file:
            // scheme URIs or local files.
            NavigateCommand = new RelayCommand(
                () => ShellHelpers.OpenInShell(Link.ToString()),
                () => Link is not null);
        }

        UpdateProperty(_initProperties);
    }
}
