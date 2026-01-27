// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI;

internal sealed class RunHistoryService : IRunHistoryService
{
    private readonly AppStateService _appStateService;

    private AppStateModel AppState => _appStateService.CurrentSettings;

    public RunHistoryService(AppStateService appStateService)
    {
        _appStateService = appStateService;
    }

    public IReadOnlyList<string> GetRunHistory()
    {
        if (AppState.RunHistory.Count == 0)
        {
            var history = Microsoft.Terminal.UI.RunHistory.CreateRunHistory();
            AppState.RunHistory.AddRange(history);
        }

        return AppState.RunHistory;
    }

    public void ClearRunHistory()
    {
        AppState.RunHistory.Clear();
    }

    public void AddRunHistoryItem(string item)
    {
        // insert at the beginning of the list
        if (string.IsNullOrWhiteSpace(item))
        {
            return; // Do not add empty or whitespace items
        }

        AppState.RunHistory.Remove(item);

        // Add the item to the front of the history
        AppState.RunHistory.Insert(0, item);

        _appStateService.SaveSettings(AppState);
    }
}
