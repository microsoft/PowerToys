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
    public AppStateModel State => Volatile.Read(ref _state);

    /// <inheritdoc/>
    public event TypedEventHandler<IAppStateService, AppStateModel>? StateChanged;

    /// <inheritdoc/>
    public void Save() => UpdateState(s => s);

    /// <inheritdoc/>
    public void UpdateState(Func<AppStateModel, AppStateModel> transform)
    {
        AppStateModel snapshot;
        AppStateModel updated;
        do
        {
            snapshot = Volatile.Read(ref _state);
            updated = transform(snapshot);
        }
        while (Interlocked.CompareExchange(ref _state, updated, snapshot) != snapshot);

        _persistence.Save(updated, _filePath, JsonSerializationContext.Default.AppStateModel);
        StateChanged?.Invoke(this, updated);
    }

    private string StateJsonPath()
    {
        var directory = _appInfoService.ConfigDirectory;
        return Path.Combine(directory, "state.json");
    }
}
