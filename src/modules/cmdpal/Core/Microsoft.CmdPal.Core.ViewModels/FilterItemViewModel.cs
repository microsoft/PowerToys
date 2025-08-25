// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FilterItemViewModel : ExtensionObjectViewModel, IFilterItemViewModel
{
    private readonly ExtensionObject<IFilter> _model;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IconInfoViewModel Icon { get; set; } = new(null);

    internal InitializedState Initialized { get; private set; } = InitializedState.Uninitialized;

    protected bool IsInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.Initialized);

    public bool IsInErrorState => Initialized.HasFlag(InitializedState.Error);

    public FilterItemViewModel(IFilter filter, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(filter);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        var filter = _model.Unsafe;
        if (filter == null)
        {
            return; // throw?
        }

        Id = filter.Id;
        Name = filter.Name;
        Icon = new(filter.Icon);
        if (Icon is not null)
        {
            Icon.InitializeProperties();
        }

        UpdateProperty(nameof(Id));
        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Icon));
    }
}
