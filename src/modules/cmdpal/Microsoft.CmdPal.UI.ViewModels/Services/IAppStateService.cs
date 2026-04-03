// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Manages the lifecycle of <see cref="AppStateModel"/>: load, save, and change notification.
/// </summary>
public interface IAppStateService
{
    /// <summary>
    /// Gets the current application state instance.
    /// </summary>
    AppStateModel State { get; }

    /// <summary>
    /// Persists the current state to disk and raises <see cref="StateChanged"/>.
    /// </summary>
    void Save();

    /// <summary>
    /// Atomically applies a transformation to the current state, persists the result,
    /// and raises <see cref="StateChanged"/>.
    /// </summary>
    void UpdateState(Func<AppStateModel, AppStateModel> transform);

    /// <summary>
    /// Raised after state has been saved to disk.
    /// </summary>
    event TypedEventHandler<IAppStateService, AppStateModel> StateChanged;
}
