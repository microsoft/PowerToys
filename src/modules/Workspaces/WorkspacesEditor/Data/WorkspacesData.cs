// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using WorkspacesEditor.Utils;

using static WorkspacesEditor.Data.ProjectData;
using static WorkspacesEditor.Data.WorkspacesData;

namespace WorkspacesEditor.Data
{
    public class WorkspacesData : WorkspacesEditorData<WorkspacesListWrapper>
    {
        public string File => FolderUtils.DataFolder() + "\\workspaces.json";

        public struct WorkspacesListWrapper
        {
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
}
