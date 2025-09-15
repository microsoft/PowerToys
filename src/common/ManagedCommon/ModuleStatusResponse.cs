// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace ManagedCommon
{
    public sealed class ModuleStatusResponse
    {
        public string ModuleName { get; set; } = string.Empty;

        public string[] AvailableEndpoints { get; set; } = [];
    }
}
