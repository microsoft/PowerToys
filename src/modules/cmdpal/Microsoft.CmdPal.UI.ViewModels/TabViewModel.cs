// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// View model for a single tab within a <see cref="TabbedPageViewModel"/>. Wraps
/// an extension-provided <see cref="ITab"/> and surfaces its chrome (title, icon,
/// observable badge) so the tab strip can render before the hosted page is
/// initialized. The hosted <see cref="IPage"/> itself is exposed via
/// <see cref="Page"/> and only turned into a child view model lazily, the first
/// time the tab becomes active.
/// </summary>
public partial class TabViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<ITab> _model;

    /// <summary>
    /// Gets the stable identity for this tab, used to preserve the active tab
    /// across dynamic tab-set updates. This is the hosted page's <c>Id</c> when
    /// available; otherwise it falls back to the tab title.
    /// </summary>
    public string TabId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Badge { get; private set; } = string.Empty;

    public bool HasBadge => !string.IsNullOrEmpty(Badge);

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public bool HasIcon => Icon.IsSet;

    /// <summary>
    /// Gets the raw extension page hosted by this tab. The host turns this into
    /// a child <see cref="PageViewModel"/> through the page view model factory
    /// when the tab is first activated.
    /// </summary>
    public IPage? Page { get; private set; }

    public TabViewModel(ITab tab, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(tab);
    }

    public override void InitializeProperties()
    {
        var tab = _model.Unsafe;
        if (tab is null)
        {
            return;
        }

        Page = tab.Page;

        var title = tab.Title;
        if (string.IsNullOrEmpty(title))
        {
            // Fall back to the hosted page's own name/title so the strip is
            // never blank when the extension didn't set a tab title.
            title = Page?.Title;
            if (string.IsNullOrEmpty(title))
            {
                title = Page?.Name;
            }
        }

        Title = title ?? string.Empty;
        Badge = tab.Badge ?? string.Empty;

        var pageId = Page?.Id;
        TabId = string.IsNullOrEmpty(pageId) ? Title : pageId;

        Icon = new(tab.Icon);
        Icon.InitializeProperties();

        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Badge));
        UpdateProperty(nameof(HasBadge));
        UpdateProperty(nameof(Icon));
        UpdateProperty(nameof(HasIcon));

        tab.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var tab = _model.Unsafe;
            if (tab is null)
            {
                return;
            }

            switch (args.PropertyName)
            {
                case nameof(Badge):
                    Badge = tab.Badge ?? string.Empty;
                    UpdateProperty(nameof(Badge));
                    UpdateProperty(nameof(HasBadge));
                    break;
                case nameof(Title):
                    Title = string.IsNullOrEmpty(tab.Title) ? Title : tab.Title;
                    UpdateProperty(nameof(Title));
                    break;
                case nameof(Icon):
                    Icon = new(tab.Icon);
                    Icon.InitializeProperties();
                    UpdateProperty(nameof(Icon));
                    UpdateProperty(nameof(HasIcon));
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        var tab = _model.Unsafe;
        if (tab is not null)
        {
            tab.PropChanged -= Model_PropChanged;
        }
    }
}
