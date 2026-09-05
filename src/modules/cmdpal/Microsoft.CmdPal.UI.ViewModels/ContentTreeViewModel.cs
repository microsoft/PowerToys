// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentTreeViewModel(ITreeContent _tree, WeakReference<IPageContext> context) :
    ContentViewModel(context)
{
    private List<ContentViewModel> _ownedChildren = [];
    private ITreeContent? _subscribedTree;

    public ExtensionObject<ITreeContent> Model { get; } = new(_tree);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public ContentViewModel? RootContent { get; protected set; }

    public ObservableCollection<ContentViewModel> Children { get; } = [];

    public bool HasChildren => Children.Count > 0;

    // This is the content that's actually bound in XAML. We needed a
    // collection, even if the collection is just a single item.
    public ObservableCollection<ContentViewModel> Root => RootContent is not null ? [RootContent] : [];

    protected override INotifyPropChanged? ObservableModel => Model.Unsafe;

    protected override void InitializeContent()
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        model.ItemsChanged += Model_ItemsChanged;
        _subscribedTree = model;

        ReplaceRoot(model.RootContent);
        FetchContent();
    }

    // Theoretically, we should unify this with the one in CommandPalettePageViewModelFactory
    // and maybe just have a ContentViewModelFactory or something
    public ContentViewModel? ViewModelFromContent(IContent content, WeakReference<IPageContext> context)
    {
        ContentViewModel? viewModel = content switch
        {
            IFormContent form => new ContentFormViewModel(form, context),
            IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
            ITreeContent tree => new ContentTreeViewModel(tree, context),
            IPlainTextContent plainText => new ContentPlainTextViewModel(plainText, context),
            IImageContent image => new ContentImageViewModel(image, context),
            _ => null,
        };
        return viewModel;
    }

    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        try
        {
            Lifetime.Run(FetchContent);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected override void FetchProperty(string propertyName)
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(RootContent):
                ReplaceRoot(model.RootContent);
                break;
        }

        UpdateProperty(propertyName);
    }

    private void ReplaceRoot(IContent? model)
    {
        var replacement = model is null ? null : ViewModelFromContent(model, PageContext);
        try
        {
            replacement?.InitializeProperties();
        }
        catch
        {
            replacement?.SafeCleanup();
            throw;
        }

        var previous = RootContent;
        RootContent = replacement;
        previous?.SafeCleanup();
        UpdateProperty(nameof(RootContent), nameof(Root));
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
                var viewModel = ViewModelFromContent(item, PageContext);
                if (viewModel is not null)
                {
                    newContent.Add(viewModel);
                    viewModel.InitializeProperties();
                }
            }
        }
        catch
        {
            newContent.ForEach(vm => vm.SafeCleanup());
            throw;
        }

        var previous = _ownedChildren;
        Volatile.Write(ref _ownedChildren, newContent);
        previous.ForEach(vm => vm.SafeCleanup());

        DoOnUiThread(
        () =>
        {
            if (Lifetime.IsClosed || !ReferenceEquals(Volatile.Read(ref _ownedChildren), newContent))
            {
                return;
            }

            ListHelpers.InPlaceUpdateList(Children, newContent);
            UpdateProperty(nameof(HasChildren));
        });
    }

    protected override void CleanupContent()
    {
        var tree = _subscribedTree;
        _subscribedTree = null;
        try
        {
            if (tree is not null)
            {
                tree.ItemsChanged -= Model_ItemsChanged;
            }
        }
        finally
        {
            RootContent?.SafeCleanup();
            RootContent = null;
            var previous = _ownedChildren;
            Volatile.Write(ref _ownedChildren, []);
            previous.ForEach(vm => vm.SafeCleanup());
            var empty = _ownedChildren;
            DoOnUiThread(() =>
            {
                if (ReferenceEquals(Volatile.Read(ref _ownedChildren), empty))
                {
                    Children.Clear();
                    UpdateProperty(nameof(HasChildren));
                }
            });
        }
    }
}
