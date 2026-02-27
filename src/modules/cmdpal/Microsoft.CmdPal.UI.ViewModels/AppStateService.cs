// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AppStateService
{
    private readonly string _filePath;
    private readonly PersistenceService _persistenceService;

    public event TypedEventHandler<AppStateModel, object?>? StateChanged;

    private AppStateModel _appStateModel;

    public AppStateModel CurrentState => _appStateModel;

    public AppStateService(
        PersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
        _filePath = _persistenceService.SettingsJsonPath("state.json");
        _appStateModel = LoadState();
    }

    private AppStateModel LoadState()
    {
        return _persistenceService.LoadObject<AppStateModel>(_filePath, JsonSerializationContext.Default.AppStateModel!);
    }

    public void SaveSettings(AppStateModel model)
    {
        _persistenceService.SaveObject(
                        model,
                        _filePath,
                        JsonSerializationContext.Default.AppStateModel,
                        JsonSerializationContext.Default.Options,
                        null,
                        afterWriteCallback: m => FinalizeStateSave(m));
    }

    private void FinalizeStateSave(AppStateModel model)
    {
        _appStateModel = model;
        StateChanged?.Invoke(model, null);
    }
}
