// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ConfirmResultViewModel(IConfirmationArgs _args, WeakReference<IPageContext> context) :
    ExtensionObjectViewModel(context)
{
    public ExtensionObject<IConfirmationArgs> Model { get; } = new(_args);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsPrimaryCommandCritical { get; private set; }

    public CommandViewModel PrimaryCommand { get; private set; } = new(null, context);

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return;
        }

        Title = model.Title;
        Description = model.Description;
        IsPrimaryCommandCritical = model.IsPrimaryCommandCritical;
        PrimaryCommand = new(model.PrimaryCommand, PageContext);
        PrimaryCommand.InitializeProperties();

        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Description));
        UpdateProperty(nameof(IsPrimaryCommandCritical));
        UpdateProperty(nameof(PrimaryCommand));
    }
}
