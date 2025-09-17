// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Providers
{
    public sealed class ProviderInfo
    {
        public ProviderInfo(string displayName, string version)
        {
            DisplayName = displayName ?? string.Empty;
            Version = version ?? string.Empty;
        }

        public string DisplayName { get; }

        public string Version { get; }
    }
}
