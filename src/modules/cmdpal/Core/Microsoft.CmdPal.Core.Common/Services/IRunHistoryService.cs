// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Services;

public interface IRunHistoryService
{
    /// <summary>
    /// Gets the run history.
    /// </summary>
    /// <returns>A list of run history items.</returns>
    IReadOnlyList<string> GetRunHistory();

    /// <summary>
    /// Clears the run history.
    /// </summary>
    void ClearRunHistory();

    /// <summary>
    /// Adds a run history item.
    /// </summary>
    /// <param name="item">The run history item to add.</param>
    void AddRunHistoryItem(string item);
}

public interface ITelemetryService
{
    void LogRunQuery(string query, int resultCount, ulong durationMs);

    void LogRunCommand(string command, bool asAdmin, bool success);

    void LogOpenUri(string uri, bool isWeb, bool success);
}
