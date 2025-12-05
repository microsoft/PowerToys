// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.UI.Models;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AppStateModel : ObservableObject
{
    private static string _filePath;

    public event TypedEventHandler<AppStateModel, object?>? StateChanged;

    ///////////////////////////////////////////////////////////////////////////
    // STATE HERE
    // Make sure that you make the setters public (JsonSerializer.Deserialize will fail silently otherwise)!
    // Make sure that any new types you add are added to JsonSerializationContext!
    public List<string> RunHistory { get; set; } = [];

    // END STATE
    ///////////////////////////////////////////////////////////////////////////

    static AppStateModel()
    {
        _filePath = PersistenceService.SettingsJsonPath("state.json");
    }

    public static AppStateModel LoadState(ILogger logger)
    {
        return PersistenceService.LoadObject<AppStateModel>(_filePath, JsonSerializationContext.Default.AppStateModel!, logger);
    }

    public static void SaveState(AppStateModel model, ILogger logger)
    {
        try
        {
            PersistenceService.SaveObject(
                    model,
                    _filePath,
                    JsonSerializationContext.Default.AppStateModel!,
                    JsonSerializationContext.Default.AppStateModel!.Options,
                    beforeWriteMutation: null,
                    afterWriteCallback: m => m.StateChanged?.Invoke(m, null),
                    logger);
        }
        catch (Exception ex)
        {
            Log_SaveStateFailure(logger, _filePath, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save application state to '{filePath}'.")]
    static partial void Log_SaveStateFailure(ILogger logger, string filePath, Exception exception);
}
