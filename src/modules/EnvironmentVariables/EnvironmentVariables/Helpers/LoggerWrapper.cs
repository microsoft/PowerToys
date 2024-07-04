// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using EnvironmentVariablesUILib.Helpers;
using ManagedCommon;

namespace EnvironmentVariables
{
    internal sealed class LoggerWrapper : ILogger
    {
        public void LogDebug(string message)
        {
            Logger.LogDebug(message);
        }

        public void LogError(string message)
        {
            Logger.LogError(message);
        }

        public void LogError(string message, Exception ex)
        {
            Logger.LogError(message, ex);
        }

        public void LogInfo(string message)
        {
            Logger.LogInfo(message);
        }

        public void LogTrace()
        {
            Logger.LogTrace();
        }

        public void LogWarning(string message)
        {
            Logger.LogWarning(message);
        }
    }
}
