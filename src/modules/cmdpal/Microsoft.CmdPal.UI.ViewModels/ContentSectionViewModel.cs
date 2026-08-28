// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentSectionViewModel : ObservedContentViewModel<ISectionContent>
{
    private readonly ContentCollectionViewModel _children;

    public ObservableCollection<ContentViewModel> VisibleContent { get; } = [];

    public string Title { get; private set; } = string.Empty;

    public int PreviewItemCount { get; private set; } = -1;

    // This state belongs to the host, not the extension. Retaining this view model
    // for an unchanged content object also retains the user's expansion choice.
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            RefreshVisibleContent();
            UpdateProperty(nameof(IsExpanded));
        }
    }

    public int HiddenItemCount => PreviewItemCount < 0 ? 0 : Math.Max(0, _children.Items.Count - PreviewItemCount);

    public bool CanExpand => HiddenItemCount > 0;

    public ContentSectionViewModel(ISectionContent model, WeakReference<IPageContext> context)
        : base(model, context)
    {
        _children = new(context);
        _children.Updated += Children_Updated;
    }

    protected override void ReadProperties()
    {
        Title = Model.Title ?? string.Empty;
        PreviewItemCount = Model.PreviewItemCount;
        UpdateProperty(nameof(Title), nameof(PreviewItemCount));
        _children.Update(Model.Content);
    }

    private void Children_Updated(object? sender, EventArgs e) => RefreshVisibleContent();

    private void RefreshVisibleContent()
    {
        var visible = IsExpanded || PreviewItemCount < 0
            ? _children.Items.ToList()
            : _children.Items.Take(PreviewItemCount).ToList();
        ListHelpers.InPlaceUpdateList(VisibleContent, visible);
        UpdateProperty(nameof(HiddenItemCount), nameof(CanExpand));
    }

    protected override void UnsafeCleanup()
    {
        _children.Updated -= Children_Updated;
        _children.SafeCleanup();
        DoOnUiThread(() => VisibleContent.Clear());
        base.UnsafeCleanup();
    }
}
