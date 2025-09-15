// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

namespace ManagedCommon
{
    public sealed class GlobalStatusResponse
    {
        public string Application { get; set; } = string.Empty;

        public string? Version { get; set; }

        public string Status { get; set; } = string.Empty;

        public int RegisteredModules { get; set; }

        public Dictionary<string, ModuleStatusResponse> Modules { get; set; } = [];

        public DateTimeOffset Timestamp { get; set; }
    }
}
