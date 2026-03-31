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
    public RecentCommandsManager RecentCommands { get; init; } = new();

    public ImmutableList<string> RunHistory { get; init; } = ImmutableList<string>.Empty;

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    public AppStateModel()
    {
    }

    [JsonConstructor]
    public AppStateModel(
        RecentCommandsManager recentCommands,
        ImmutableList<string> runHistory)
    {
        RecentCommands = recentCommands ?? new();
        RunHistory = runHistory ?? ImmutableList<string>.Empty;
    }
}
