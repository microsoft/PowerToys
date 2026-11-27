// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ConfirmResultViewModel : ExtensionObjectViewModel
{
    private readonly IConfirmationArgs _args;

    public ConfirmResultViewModel(IConfirmationArgs args, WeakReference<IPageContext> context)
        : this(args, context, null)
    {
    }

    public ConfirmResultViewModel(
        IConfirmationArgs args,
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext)
        : base(context)
    {
        _args = args;
        InheritFallbackContext(fallbackContext);
        Model = new(args);
        PrimaryCommand = ShareFallbackContext(new CommandViewModel(null, context));
    }

    public ExtensionObject<IConfirmationArgs> Model { get; }

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsPrimaryCommandCritical { get; private set; }

    public CommandViewModel PrimaryCommand { get; private set; }

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        Title = model.Title;
        Description = model.Description;
        IsPrimaryCommandCritical = model.IsPrimaryCommandCritical;
        PrimaryCommand = ShareFallbackContext(new CommandViewModel(model.PrimaryCommand, PageContext));
        PrimaryCommand.InitializeProperties();

        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Description));
        UpdateProperty(nameof(IsPrimaryCommandCritical));
        UpdateProperty(nameof(PrimaryCommand));
    }
}
