// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        State = _persistence.Load(_filePath, JsonSerializationContext.Default.AppStateModel);
    }

    /// <inheritdoc/>
    public AppStateModel State { get; private set; }

    /// <inheritdoc/>
    public event TypedEventHandler<IAppStateService, AppStateModel>? StateChanged;

    /// <inheritdoc/>
    public void Save()
    {
        _persistence.Save(State, _filePath, JsonSerializationContext.Default.AppStateModel);
        StateChanged?.Invoke(this, State);
    }

    private string StateJsonPath()
    {
        var directory = _appInfoService.ConfigDirectory;
        return Path.Combine(directory, "state.json");
    }
}
