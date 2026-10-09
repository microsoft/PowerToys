// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace HostsUILib.Helpers
{
    public interface ILogger
    {
        public void LogError(string message);

        public void LogError(string message, Exception ex);

        public void LogWarning(string message);

        public void LogInfo(string message);

        public void LogDebug(string message);

        public void LogTrace();
    }
}
