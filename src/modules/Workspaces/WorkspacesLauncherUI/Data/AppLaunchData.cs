// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Workspaces.Data;
using WorkspacesLauncherUI.Utils;

using static WorkspacesLauncherUI.Data.AppLaunchData;
using static WorkspacesLauncherUI.Data.AppLaunchInfoData;
using static WorkspacesLauncherUI.Data.AppLaunchInfosData;

namespace WorkspacesLauncherUI.Data
{
    internal sealed class AppLaunchData : WorkspacesEditorData<AppLaunchDataWrapper>
    {
        public static string File
        {
            get
            {
                return FolderUtils.DataFolder() + "\\launch-workspaces.json";
            }
        }

        public struct AppLaunchDataWrapper
        {
            [JsonPropertyName("apps")]
            public AppLaunchInfoListWrapper AppLaunchInfos { get; set; }

            [JsonPropertyName("processId")]
            public int LauncherProcessID { get; set; }
        }
    }
}
