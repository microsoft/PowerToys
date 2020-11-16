// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.Service
{
    public class ServiceResult
    {
        public string ServiceName { get; }

        public string DisplayName { get; }

        public bool IsRunning { get; }

        public ServiceResult(string serviceName, string displayName, bool isRunning)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            IsRunning = isRunning;
        }
    }
}
