// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentTreeViewModel(ITreeContent _tree, WeakReference<IPageContext> context) :
    ContentViewModel(context)
{
    public ExtensionObject<ITreeContent> Model { get; } = new(_tree);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public ContentViewModel? RootContent { get; protected set; }

    public ObservableCollection<ContentViewModel> Children { get; } = [];

    public bool HasChildren => Children.Count > 0;

    // This is the content that's actually bound in XAML. We needed a
    // collection, even if the collection is just a single item.
    public ObservableCollection<ContentViewModel> Root => [RootContent];

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return;
        }

        var root = model.RootContent;
        if (root != null)
        {
            RootContent = ContentPageViewModel.ViewModelFromContent(root, PageContext);
            RootContent?.InitializeProperties();
            UpdateProperty(nameof(RootContent));
            UpdateProperty(nameof(Root));
        }

        FetchContent();
        model.PropChanged += Model_PropChanged;
        model.ItemsChanged += Model_ItemsChanged;
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchContent();

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propName = args.PropertyName;
            FetchProperty(propName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected void FetchProperty(string propertyName)
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(RootContent):
                var root = model.RootContent;
                if (root != null)
                {
                    RootContent = ContentPageViewModel.ViewModelFromContent(root, PageContext);
                }
                else
                {
                    root = null;
                }

                UpdateProperty(nameof(Root));

                break;
        }

        UpdateProperty(propertyName);
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchContent()
    {
        List<ContentViewModel> newContent = [];
        try
        {
            var newItems = Model.Unsafe!.GetChildren();

            foreach (var item in newItems)
            {
                var viewModel = ContentPageViewModel.ViewModelFromContent(item, PageContext);
                if (viewModel != null)
                {
                    viewModel.InitializeProperties();
                    newContent.Add(viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
            throw;
        }

        // Now, back to a UI thread to update the observable collection
        DoOnUiThread(
        () =>
        {
            ListHelpers.InPlaceUpdateList(Children, newContent);
        });

        UpdateProperty(nameof(HasChildren));
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();
        RootContent?.SafeCleanup();
        foreach (var item in Children)
        {
            item.SafeCleanup();
        }

        Children.Clear();
        var model = Model.Unsafe;
        if (model != null)
        {
            model.PropChanged -= Model_PropChanged;
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }
}
