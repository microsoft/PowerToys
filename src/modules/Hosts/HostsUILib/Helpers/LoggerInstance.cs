// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace HostsUILib.Helpers
{
    public static class LoggerInstance
    {
        private static ILogger _logger = NullLogger.Instance;

        public static ILogger Logger
        {
            get => _logger;
            set => _logger = value ?? NullLogger.Instance;
        }
    }
}
