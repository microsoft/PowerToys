// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class OpenedPathsList
    {
        [JsonPropertyName("workspaces3")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
        public List<dynamic> Workspaces3 { get; set; }

        [JsonPropertyName("entries")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
        public List<VSCodeWorkspaceEntry> Entries { get; set; }
    }
}
