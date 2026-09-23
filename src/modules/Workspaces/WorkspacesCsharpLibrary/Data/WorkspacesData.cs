// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using WorkspacesCsharpLibrary.Utils;
using static WorkspacesCsharpLibrary.Data.WorkspacesData;

namespace WorkspacesCsharpLibrary.Data;

public class WorkspacesData : WorkspacesEditorData<WorkspacesListWrapper>
{
    public struct WorkspacesListWrapper
    {
        [JsonPropertyName("workspaces")]
        public List<ProjectWrapper> Workspaces { get; set; }
    }

    public enum OrderBy
    {
        LastViewed = 0,
        Created = 1,
        Name = 2,
        Unknown = 3,
    }
}
