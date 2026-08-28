// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentTextViewModel(ITextContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<ITextContent>(model, context)
{
    public string Text { get; private set; } = string.Empty;

    protected override void ReadProperties()
    {
        Text = Model.Text ?? string.Empty;
        UpdateProperty(nameof(Text));
    }
}
