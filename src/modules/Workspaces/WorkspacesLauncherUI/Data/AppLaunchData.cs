// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Workspaces.Data;

using static WorkspacesLauncherUI.Data.AppLaunchData;
using static WorkspacesLauncherUI.Data.AppLaunchInfosData;

namespace WorkspacesLauncherUI.Data
{
    public class AppLaunchData : WorkspacesUIData<AppLaunchDataWrapper>
    {
        public struct AppLaunchDataWrapper
        {
            [JsonPropertyName("apps")]
            public AppLaunchInfoListWrapper AppLaunchInfos { get; set; }

            [JsonPropertyName("processId")]
            public int LauncherProcessID { get; set; }
        }
    }
}
