// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentTreeViewModel : ObservedContentViewModel<ITreeContent>
{
    private readonly ContentCollectionViewModel _root;
    private readonly ContentCollectionViewModel _children;

    public ObservableCollection<ContentViewModel> Root => _root.Items;

    public ContentViewModel? RootContent => Root.FirstOrDefault();

    public ObservableCollection<ContentViewModel> Children => _children.Items;

    public bool HasChildren => Children.Count > 0;

    public ContentTreeViewModel(ITreeContent model, WeakReference<IPageContext> context)
        : base(model, context)
    {
        _root = new(context);
        _children = new(context);
        _root.Updated += Root_Updated;
        _children.Updated += Children_Updated;
    }

    protected override void SubscribeToModel()
    {
        base.SubscribeToModel();
        Model.ItemsChanged += Model_ItemsChanged;
    }

    protected override void ReadProperties()
    {
        var root = Model.RootContent;
        _root.Update(root is null ? [] : [root]);
        _children.Update(Model.GetChildren());
    }

    private void Root_Updated(object? sender, EventArgs e) => UpdateProperty(nameof(RootContent));

    private void Children_Updated(object? sender, EventArgs e) => UpdateProperty(nameof(HasChildren));

    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => RefreshProperties();

    protected override void UnsubscribeFromModel()
    {
        try
        {
            Model.ItemsChanged -= Model_ItemsChanged;
        }
        finally
        {
            base.UnsubscribeFromModel();
        }
    }

    protected override void UnsafeCleanup()
    {
        try
        {
            // Stop callbacks before revoking events or disposing child observers.
            base.UnsafeCleanup();
        }
        finally
        {
            _root.Updated -= Root_Updated;
            _children.Updated -= Children_Updated;
            _root.SafeCleanup();
            _children.SafeCleanup();
        }
    }
}
