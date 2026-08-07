// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using static WorkspacesLauncherUI.Data.AppLaunchInfoData;
using static WorkspacesLauncherUI.Data.AppLaunchInfosData;

namespace WorkspacesLauncherUI.Data
{
    public class AppLaunchInfosData : WorkspacesCsharpLibrary.Data.WorkspacesEditorData<AppLaunchInfoListWrapper>
    {
        public struct AppLaunchInfoListWrapper
        {
            [JsonPropertyName("appLaunchInfos")]
            public List<AppLaunchInfoWrapper> AppLaunchInfoList { get; set; }
        }
    }
}
