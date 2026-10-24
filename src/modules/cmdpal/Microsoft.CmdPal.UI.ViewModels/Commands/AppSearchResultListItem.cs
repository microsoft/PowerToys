// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

/// <summary>Shows a matched shortcut for a saved app command while retaining its pin actions and provider context.</summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed partial class AppSearchResultListItem : IListItem, IExtendedAttributesProvider, ICommandContextSource
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add
        {
            Presentation.PropChanged += value;
            Source.PropChanged += value;
        }

        remove
        {
            Presentation.PropChanged -= value;
            Source.PropChanged -= value;
        }
    }

    /// <summary>Gets the saved command whose pin actions and provider context are preserved.</summary>
    public TopLevelViewModel Source { get; }

    /// <summary>Gets the matched shortcut's query-specific presentation and canonical app command.</summary>
    public AppListItem Presentation { get; }

    AppExtensionHost? ICommandContextSource.ExtensionHost => Source.ExtensionHost;

    ICommandProviderContext? ICommandContextSource.ProviderContext => Source.ProviderContext;

    public ICommand? Command => Presentation.Command;

    public IContextItem?[] MoreCommands => Presentation.GetShortcutCommands(((IListItem)Source).MoreCommands);

    public IIconInfo? Icon => Presentation.Icon;

    public string Title => Presentation.Title;

    public string Subtitle => Presentation.Subtitle;

    public ITag[] Tags => ((IListItem)Source).Tags;

    public IDetails? Details => Presentation.Details;

    public string Section => ((IListItem)Source).Section;

    public string TextToSuggest => Presentation.TextToSuggest;

    private AppSearchResultListItem(TopLevelViewModel source, AppListItem presentation)
    {
        Source = source;
        Presentation = presentation;
    }

    /// <summary>Reuses a saved command's previous result when its matching shortcut has not changed.</summary>
    internal static AppSearchResultListItem CreateOrReuse(
        IReadOnlyList<RoScored<IListItem>>? previous,
        TopLevelViewModel source,
        AppListItem presentation)
    {
        if (previous is not null)
        {
            foreach (var result in previous)
            {
                if (result.Item is AppSearchResultListItem existing
                    && ReferenceEquals(existing.Source, source)
                    && ReferenceEquals(existing.Presentation, presentation))
                {
                    return existing;
                }
            }
        }

        return new AppSearchResultListItem(source, presentation);
    }

    /// <inheritdoc />
    public IDictionary<string, object?> GetProperties()
    {
        return Source.GetProperties();
    }
}
