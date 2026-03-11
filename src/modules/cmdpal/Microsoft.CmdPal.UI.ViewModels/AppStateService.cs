// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AppStateService
{
    private const string FileName = "state.json";

    private readonly PersistenceService _persistenceService;
    private AppStateModel _appStateModel;

    public event TypedEventHandler<AppStateModel, object?>? StateChanged;

    public AppStateModel CurrentSettings => _appStateModel;

    public AppStateService(PersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
        _appStateModel = LoadState();
    }

    private AppStateModel LoadState()
    {
        return _persistenceService.LoadObject<AppStateModel>(FileName, JsonSerializationContext.Default.AppStateModel!);
    }

    public void SaveSettings(AppStateModel model)
    {
        _persistenceService.SaveObject(
                        model,
                        FileName,
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
