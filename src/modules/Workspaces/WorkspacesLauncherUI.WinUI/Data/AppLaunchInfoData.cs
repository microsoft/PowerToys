// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using static WorkspacesLauncherUI.Data.AppLaunchInfoData;

namespace WorkspacesLauncherUI.Data
{
    public class AppLaunchInfoData : WorkspacesCsharpLibrary.Data.WorkspacesEditorData<AppLaunchInfoWrapper>
    {
        public struct AppLaunchInfoWrapper
        {
            [JsonPropertyName("application")]
            public ApplicationWrapper Application { get; set; }

            [JsonPropertyName("state")]
            public LaunchingState State { get; set; }
        }
    }
}
