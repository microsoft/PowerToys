// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsLinkViewModel : DetailsElementViewModel
{
    private static readonly string[] _initProperties = [
        nameof(Text),
        nameof(Link),
        nameof(IsLink),
        nameof(IsText),
        nameof(NavigateCommand)];

    private readonly ExtensionObject<IDetailsLink> _dataModel;

    public DetailsLinkViewModel(IDetailsElement detailsElement, WeakReference<IPageContext> context)
        : this(detailsElement, context, null)
    {
    }

    internal DetailsLinkViewModel(
        IDetailsElement detailsElement,
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext)
        : base(detailsElement, context, fallbackContext)
    {
        _dataModel = new(detailsElement.Data as IDetailsLink);
    }

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
                () =>
                {
                    using var operationLease = FallbackContext?.AcquireSnapshotLease();
                    if (FallbackContext?.HasSnapshotLease == true && operationLease is null)
                    {
                        return;
                    }

                    if (FallbackContext?.CanInvoke != false)
                    {
                        ShellHelpers.OpenInShell(Link.ToString());
                    }
                },
                () => Link is not null && FallbackContext?.CanInvoke != false);
        }

        UpdateProperty(_initProperties);
    }
}
