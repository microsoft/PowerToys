// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CmdPal.Common.Services;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default implementation of <see cref="IAppStateService"/>.
/// Handles loading, saving, and change notification for <see cref="AppStateModel"/>.
/// </summary>
public sealed class AppStateService : IAppStateService
{
    private readonly IPersistenceService _persistence;
    private readonly IApplicationInfoService _appInfoService;
    private readonly string _filePath;

    public AppStateService(IPersistenceService persistence, IApplicationInfoService appInfoService)
    {
        _persistence = persistence;
        _appInfoService = appInfoService;
        _filePath = StateJsonPath();
        _state = _persistence.Load(_filePath, JsonSerializationContext.Default.AppStateModel);
    }

    private AppStateModel _state;

    /// <inheritdoc/>
    public AppStateModel State => _state;

    /// <inheritdoc/>
    public event TypedEventHandler<IAppStateService, AppStateModel>? StateChanged;

    /// <inheritdoc/>
    public void Save() => UpdateState(s => s);

    /// <inheritdoc/>
    public void UpdateState(Func<AppStateModel, AppStateModel> transform)
    {
        var newState = transform(_state);
        Interlocked.Exchange(ref _state, newState);
        _persistence.Save(newState, _filePath, JsonSerializationContext.Default.AppStateModel);
        StateChanged?.Invoke(this, newState);
    }

    private string StateJsonPath()
    {
        var directory = _appInfoService.ConfigDirectory;
        return Path.Combine(directory, "state.json");
    }
}
