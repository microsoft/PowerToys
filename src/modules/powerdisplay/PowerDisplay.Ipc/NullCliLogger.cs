// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Ipc;

internal sealed class NullCliLogger : ICliLogger
{
    public static NullCliLogger Instance { get; } = new();

    private NullCliLogger()
    {
    }

    public void LogInfo(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void LogError(string message)
    {
    }
}
