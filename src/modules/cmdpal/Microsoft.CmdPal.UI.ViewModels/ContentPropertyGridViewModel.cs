// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentPropertyGridViewModel(IPropertyGridContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<IPropertyGridContent>(model, context)
{
    private readonly ContentCollectionViewModel _properties = new(context);

    public ObservableCollection<ContentViewModel> Properties => _properties.Items;

    protected override void ReadProperties() => _properties.Update(Model.Properties);

    protected override void UnsafeCleanup()
    {
        _properties.SafeCleanup();
        base.UnsafeCleanup();
    }
}
