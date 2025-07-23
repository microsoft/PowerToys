// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FilterItemViewModel : ExtensionObjectViewModel, IFilterItemViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public IconInfoViewModel Icon { get; set; }

    public FilterItemViewModel(IFilter filter, WeakReference<IPageContext> context)
        : base(context)
    {
        Id = filter.Id;
        Name = filter.Name;
        Icon = new(filter.Icon);
    }

    public override void InitializeProperties()
    {
        if (Icon is not null)
        {
            Icon.InitializeProperties();
        }
    }
}
