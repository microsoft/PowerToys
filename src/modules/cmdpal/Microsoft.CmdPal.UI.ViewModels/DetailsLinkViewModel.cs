// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsLinkViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    private readonly ExtensionObject<IDetailsLink> _dataModel =
        new(_detailsElement.Data as IDetailsLink);

    public string Text { get; private set; } = string.Empty;

    public Uri? Link { get; private set; }

    public bool IsLink => Link != null;

    public bool IsText => !IsLink;

    public override void InitializeProperties()
    {
        base.InitializeProperties();
        var model = _dataModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Text = model.Text ?? string.Empty;
        Link = model.Link;
        if (string.IsNullOrEmpty(Text) && Link != null)
        {
            Text = Link.ToString();
        }

        UpdateProperty(nameof(Text));
        UpdateProperty(nameof(Link));
        UpdateProperty(nameof(IsLink));
        UpdateProperty(nameof(IsText));
    }
}
