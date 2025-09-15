// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

namespace ManagedCommon
{
    public sealed class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public IEnumerable<string>? RegisteredModules { get; set; }

        public string[]? AvailableEndpoints { get; set; }

        public int? StatusCode { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
