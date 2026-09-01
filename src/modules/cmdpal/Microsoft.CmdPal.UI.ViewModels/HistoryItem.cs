// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels;

public record HistoryItem
{
    public required string CommandId { get; init; }

    public required int Uses { get; init; }

    /// <summary>
    /// Gets the moment this command was last invoked. Persisted so ranking can apply real
    /// time-decay instead of the previous list-position heuristic. History written before
    /// this field existed deserializes to <c>default(DateTimeOffset)</c>;
    /// <see cref="RecentCommandsManager"/> treats that sentinel as a mild backdate so
    /// day-one ordering degrades gracefully to Uses-ordering rather than going all-equal.
    /// </summary>
    public DateTimeOffset LastUsed { get; init; }
}
