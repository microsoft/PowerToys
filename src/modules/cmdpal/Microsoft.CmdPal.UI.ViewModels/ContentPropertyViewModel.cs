// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentPropertyViewModel(IPropertyContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<IPropertyContent>(model, context)
{
    private readonly ContentCollectionViewModel _value = new(context);

    public string Label { get; private set; } = string.Empty;

    public ObservableCollection<ContentViewModel> ValueContent => _value.Items;

    protected override void ReadProperties()
    {
        Label = Model.Label ?? string.Empty;
        UpdateProperty(nameof(Label));
        var value = Model.Value;
        _value.Update(value is null ? [] : [value]);
    }

    protected override void UnsafeCleanup()
    {
        _value.SafeCleanup();
        base.UnsafeCleanup();
    }
}
