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

using static WorkspacesLauncherUI.Data.AppLaunchInfoData;

namespace WorkspacesLauncherUI.Data
{
    public class AppLaunchInfoData : WorkspacesEditorData<AppLaunchInfoWrapper>
    {
        public struct AppLaunchInfoWrapper
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("path")]
            public string Path { get; set; }

            [JsonPropertyName("state")]
            public string State { get; set; }
        }
    }
}
