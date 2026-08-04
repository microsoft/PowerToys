// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Provides the stable application list items shared by the All Apps page and other app consumers.
/// </summary>
/// <remarks>
/// The source owns list-item projection, preserves unchanged list-item instances, and keeps page
/// search and filter state out of consumers that need the complete visible application list.
/// </remarks>
public interface IAppListItemSource : IDisposable
{
    /// <summary>
    /// Raised after the published items, presentation settings, result limit, or loading state changes.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets an atomic snapshot of visible and hidden application list items.
    /// </summary>
    AppListItemSnapshot GetSnapshot();

    /// <summary>
    /// Gets a value indicating whether the initial catalog load or a user-requested refresh is active.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Gets the configured maximum number of application results for consumers that embed the list.
    /// </summary>
    int TopLevelResultLimit { get; }

    /// <summary>
    /// Requests a full background refresh of the application catalog.
    /// </summary>
    Task RefreshAsync();
}

/// <summary>
/// Represents one atomic publication of visible and hidden application list items.
/// </summary>
public sealed class AppListItemSnapshot
{
    /// <summary>
    /// Initializes an atomic visible and hidden list-item snapshot.
    /// </summary>
    /// <param name="visibleItems">Applications eligible for normal user-facing views.</param>
    /// <param name="hiddenItems">Applications explicitly hidden by the user.</param>
    /// <param name="patternHiddenItems">Applications hidden by global exclusion patterns.</param>
    public AppListItemSnapshot(
        IReadOnlyList<AppListItem> visibleItems,
        IReadOnlyList<AppListItem> hiddenItems,
        IReadOnlyList<AppListItem>? patternHiddenItems = null)
    {
        VisibleItems = visibleItems ?? throw new ArgumentNullException(nameof(visibleItems));
        HiddenItems = hiddenItems ?? throw new ArgumentNullException(nameof(hiddenItems));
        PatternHiddenItems = patternHiddenItems ?? [];
    }

    /// <summary>
    /// Gets applications eligible for normal user-facing views.
    /// </summary>
    public IReadOnlyList<AppListItem> VisibleItems { get; }

    /// <summary>
    /// Gets applications explicitly hidden by the user.
    /// </summary>
    public IReadOnlyList<AppListItem> HiddenItems { get; }

    /// <summary>Gets applications hidden by global name or path exclusion patterns.</summary>
    public IReadOnlyList<AppListItem> PatternHiddenItems { get; }
}
