// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record AppStateModel
{
    ///////////////////////////////////////////////////////////////////////////
    // STATE HERE
    // Make sure that any new types you add are added to JsonSerializationContext!
    private RecentCommandsManager? _recentCommands = new();

    public RecentCommandsManager RecentCommands
    {
        get => _recentCommands ?? new();
        init => _recentCommands = value;
    }

    private ImmutableList<string>? _runHistory = ImmutableList<string>.Empty;

    public ImmutableList<string> RunHistory
    {
        get => _runHistory ?? ImmutableList<string>.Empty;
        init => _runHistory = value;
    }

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////
}
