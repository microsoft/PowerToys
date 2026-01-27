// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AppStateService
{
    private readonly ILogger logger;
    private readonly string _filePath;
    private AppStateModel _appStateModel;

    public event TypedEventHandler<AppStateModel, object?>? StateChanged;

    public AppStateModel CurrentSettings => _appStateModel;

    public AppStateService(ILogger logger)
    {
        this.logger = logger;
        _filePath = PersistenceService.SettingsJsonPath("state.json");
        _appStateModel = LoadState();
    }

    private AppStateModel LoadState()
    {
        return PersistenceService.LoadObject<AppStateModel>(_filePath, JsonSerializationContext.Default.AppStateModel!, logger);
    }

    public void SaveSettings(AppStateModel model)
    {
        PersistenceService.SaveObject(
                        model,
                        _filePath,
                        JsonSerializationContext.Default.AppStateModel,
                        JsonSerializationContext.Default.Options,
                        null,
                        afterWriteCallback: m => FinalizeStateSave(m),
                        logger);
    }

    private void FinalizeStateSave(AppStateModel model)
    {
        _appStateModel = model;
        StateChanged?.Invoke(model, null);
    }
}
