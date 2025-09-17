// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace TopToolbar.Actions
{
    public sealed class ActionContext
    {
        public string FocusedApp { get; set; } = string.Empty;

        public string Selection { get; set; } = string.Empty;

        public string WorkspaceId { get; set; } = string.Empty;

        public string Zone { get; set; } = string.Empty;

        public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Locale { get; set; } = string.Empty;

        public string NowUtcIso { get; set; } = string.Empty;
    }
}
