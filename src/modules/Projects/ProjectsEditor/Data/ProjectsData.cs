// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Projects.Data;
using ProjectsEditor.Utils;
using static ProjectsEditor.Data.ProjectData;
using static ProjectsEditor.Data.ProjectsData;

namespace ProjectsEditor.Data
{
    public class ProjectsData : ProjectsEditorData<ProjectsListWrapper>
    {
        public string File
        {
            get
            {
                return FolderUtils.DataFolder() + "\\projects.json";
            }
        }

        public struct ProjectsListWrapper
        {
            public List<ProjectWrapper> Projects { get; set; }
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
