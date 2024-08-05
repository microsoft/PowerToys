// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Projects.Data;
using static ProjectsLauncherUI.Data.AppLaunchInfoData;
using static ProjectsLauncherUI.Data.AppLaunchInfosData;

namespace ProjectsLauncherUI.Data
{
    public class AppLaunchInfosData : ProjectsEditorData<AppLaunchInfoListWrapper>
    {
        public struct AppLaunchInfoListWrapper
        {
            [JsonPropertyName("appLaunchInfos")]
            public List<AppLaunchInfoWrapper> AppLaunchInfoList { get; set; }
        }
    }
}
