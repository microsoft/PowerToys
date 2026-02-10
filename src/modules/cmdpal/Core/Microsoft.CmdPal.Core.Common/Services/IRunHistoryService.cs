// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Services;

public interface IRunHistoryService
{
    long RunCommand(string commandLine, string workingDir, bool asAdmin, ulong hwnd);

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

    /// <summary>
    /// Parses a command line into its components.
    /// </summary>
    ParseCommandlineResult ParseCommandline(string commandLine, string workingDirectory);

    string QualifyCommandLineDirectory(string commandLine, string fullFilePath, string defaultDirectory);
}

public interface ITelemetryService
{
    void LogRunQuery(string query, int resultCount, ulong durationMs);

    void LogRunCommand(string command, bool asAdmin, bool success);

    void LogOpenUri(string uri, bool isWeb, bool success);

    void LogEvent(string eventName, IDictionary<string, object>? properties = null);
}

public struct ParseCommandlineResult
{
    public int Result; // HRESULT
    public bool IsUri;
    public string FilePath;
    public string Arguments;

    public bool Success => Result == 0;
}
