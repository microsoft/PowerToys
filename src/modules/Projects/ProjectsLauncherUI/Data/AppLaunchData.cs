// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Projects.Data;
using ProjectsLauncherUI.Utils;
using static ProjectsLauncherUI.Data.AppLaunchData;
using static ProjectsLauncherUI.Data.AppLaunchInfoData;
using static ProjectsLauncherUI.Data.AppLaunchInfosData;

namespace ProjectsLauncherUI.Data
{
    internal sealed class AppLaunchData : ProjectsEditorData<AppLaunchDataWrapper>
    {
        public static string File
        {
            get
            {
                return FolderUtils.DataFolder() + "\\launch-project.json";
            }
        }

        public struct AppLaunchDataWrapper
        {
            [JsonPropertyName("apps")]
            public AppLaunchInfoListWrapper AppLaunchInfos { get; set; }

            [JsonPropertyName("processid")]
            public int LauncherProcessID { get; set; }
        }
    }
}
