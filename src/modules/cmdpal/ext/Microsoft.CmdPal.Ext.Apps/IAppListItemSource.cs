// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

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
    /// Gets a value indicating whether the initial catalog load or a user-requested refresh is active.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Gets the configured maximum number of application results for consumers that embed the list.
    /// </summary>
    int TopLevelResultLimit { get; }

    /// <summary>
    /// Gets an atomic snapshot of visible and hidden application list items.
    /// </summary>
    AppListItemSnapshot GetSnapshot();

    /// <summary>Requests a background refresh of cached execution-alias ownership without waiting.</summary>
    void RequestExecutionAliasRefresh();

    /// <summary>
    /// Requests a foreground full refresh of the application catalog and waits for that request to be handled.
    /// </summary>
    Task RefreshAsync();
}
