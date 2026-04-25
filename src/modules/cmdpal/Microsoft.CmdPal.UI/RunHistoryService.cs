// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
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
        if (_appStateService.State.RunHistory.IsEmpty)
        {
            var history = Microsoft.Terminal.UI.RunHistory.CreateRunHistory();
            _appStateService.UpdateState(state => state with
            {
                RunHistory = history.ToImmutableList(),
            });
        }

        return _appStateService.State.RunHistory;
    }

    public void ClearRunHistory()
    {
        _appStateService.UpdateState(state => state with
        {
            RunHistory = ImmutableList<string>.Empty,
        });
    }

    public void AddRunHistoryItem(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return;
        }

        _appStateService.UpdateState(state => state with
        {
            RunHistory = state.RunHistory
                .Remove(item)
                .Insert(0, item),
        });
    }
}
