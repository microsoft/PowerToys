// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI;

internal sealed class RunHistoryService : IRunHistoryService
{
    private readonly IAppStateService _appStateService;

    public RunHistoryService(IAppStateService appStateService)
    {
        _appStateService = appStateService;
    }

    public IReadOnlyList<string> GetRunHistory()
    {
        if (_appStateService.State.RunHistory.Count == 0)
        {
            var history = Microsoft.Terminal.UI.RunHistory.CreateRunHistory();
            _appStateService.State.RunHistory.AddRange(history);
        }

        return _appStateService.State.RunHistory;
    }

    public void ClearRunHistory()
    {
        _appStateService.State.RunHistory.Clear();
    }

    public void AddRunHistoryItem(string item)
    {
        // insert at the beginning of the list
        if (string.IsNullOrWhiteSpace(item))
        {
            return; // Do not add empty or whitespace items
        }

        _appStateService.State.RunHistory.Remove(item);

        // Add the item to the front of the history
        _appStateService.State.RunHistory.Insert(0, item);

        _appStateService.Save();
    }
}
