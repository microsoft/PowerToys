// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class DetailsElementViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IDetailsElement> _model;

    internal FallbackQueryContext? FallbackContext { get; }

    protected DetailsElementViewModel(IDetailsElement detailsElement, WeakReference<IPageContext> context)
        : this(detailsElement, context, null)
    {
    }

    internal DetailsElementViewModel(
        IDetailsElement detailsElement,
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext)
        : base(context)
    {
        _model = new(detailsElement);
        FallbackContext = fallbackContext;
    }

    public string Key { get; private set; } = string.Empty;

    public override void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        Key = model.Key ?? string.Empty;
        UpdateProperty(nameof(Key));
    }
}
