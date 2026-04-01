// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace ImageResizer.Cli
{
    public static class CliLogger
    {
        private static bool _initialized;

        public static void Initialize(string logSubFolder)
        {
            if (!_initialized)
            {
                Logger.InitializeLogger(logSubFolder);
                _initialized = true;
            }
        }

        public static void Info(string message) => Logger.LogInfo(message);

        public static void Warn(string message) => Logger.LogWarning(message);

        public static void Error(string message) => Logger.LogError(message);
    }
}
