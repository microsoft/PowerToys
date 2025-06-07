// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class GoToPageArgsViewModel(IGoToPageArgs _args, WeakReference<IPageContext> context) :
    ExtensionObjectViewModel(context)
{
    public ExtensionObject<IGoToPageArgs> Model { get; } = new(_args);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string PageId { get; private set; } = string.Empty;

    public NavigationMode NavigationMode { get; private set; } = NavigationMode.Push;

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return;
        }

        PageId = model.PageId;
        NavigationMode = model.NavigationMode;

        UpdateProperty(nameof(PageId));
        UpdateProperty(nameof(NavigationMode));
    }
}
