// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    public sealed class RunnerActionDescriptor
    {
        public string ActionId { get; set; } = string.Empty;

        public string ModuleKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public bool Available { get; set; }
    }
}
