// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Services;

public interface IRootPageService
{
    /// <summary>
    /// Gets the root page of the command palette. Return any IPage implementation that
    /// represents the root view of this instance of the command palette.
    /// </summary>
    Microsoft.CommandPalette.Extensions.IPage GetRootPage();

    /// <summary>
    /// Pre-loads any necessary data or state before the root page is loaded.
    /// This will be awaited before the root page and the user can do anything,
    /// so ideally it should be quick and not block the UI thread for long.
    /// </summary>
    Task PreLoadAsync();

    /// <summary>
    /// Do any loading work that can be done after the root page is loaded and
    /// displayed to the user.
    /// This is run asynchronously, on a background thread.
    /// </summary>
    Task PostLoadRootPageAsync();

    /// <summary>
    /// Called when a top-level command is performed. The context is the
    /// sender context for the invoked command. This is typically the IListItem
    /// or ICommandContextItem that was used to invoke the command.
    /// </summary>
    void OnPerformTopLevelCommand(object? context);
}
