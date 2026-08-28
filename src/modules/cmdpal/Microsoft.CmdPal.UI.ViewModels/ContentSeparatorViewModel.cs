// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentSeparatorViewModel(ISeparatorContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<ISeparatorContent>(model, context)
{
    public string Title { get; private set; } = string.Empty;

    protected override void ReadProperties()
    {
        Title = Model.Title ?? string.Empty;
        UpdateProperty(nameof(Title));
    }
}
