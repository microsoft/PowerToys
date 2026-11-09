// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace PowerDisplay.Ipc;

internal sealed class ManagedCliLogger : ICliLogger
{
    public void LogInfo(string message) => Logger.LogInfo(message);

    public void LogWarning(string message) => Logger.LogWarning(message);

    public void LogError(string message) => Logger.LogError(message);
}
