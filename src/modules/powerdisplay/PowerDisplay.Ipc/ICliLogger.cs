// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Ipc;

/// <summary>Logging boundary supplied by the hosting application.</summary>
public interface ICliLogger
{
    /// <summary>Logs an informational message.</summary>
    void LogInfo(string message);

    /// <summary>Logs a warning message.</summary>
    void LogWarning(string message);

    /// <summary>Logs an error message.</summary>
    void LogError(string message);
}
