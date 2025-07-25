// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI;

internal sealed class RunHistoryService : IRunHistoryService
{
    private readonly AppStateModel _appStateModel;

    public RunHistoryService(AppStateModel appStateModel)
    {
        _appStateModel = appStateModel;
    }

    public IReadOnlyList<string> GetRunHistory()
    {
        if (_appStateModel.RunHistory.Count == 0)
        {
            IList<string> history = Microsoft.Terminal.UI.RunHistory.CreateRunHistory();
            _appStateModel.RunHistory.AddRange(history);
        }

        return _appStateModel.RunHistory;
    }

    public void ClearRunHistory()
    {
        _appStateModel.RunHistory.Clear();
    }

    public void AddRunHistoryItem(string item)
    {
        // insert at the beginning of the list
        if (string.IsNullOrWhiteSpace(item))
        {
            return; // Do not add empty or whitespace items
        }

        _appStateModel.RunHistory.Remove(item);

        // Add the item to the front of the history
        _appStateModel.RunHistory.Insert(0, item);

        AppStateModel.SaveState(_appStateModel);
    }
}
