// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace WorkspacesCsharpLibrary.Data;

[JsonSourceGenerationOptions(MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(WorkspacesData.WorkspacesListWrapper))]
[JsonSerializable(typeof(ProjectWrapper))]
[JsonSerializable(typeof(ApplicationWrapper))]
[JsonSerializable(typeof(ApplicationWrapper.WindowPositionWrapper))]
[JsonSerializable(typeof(MonitorConfigurationWrapper))]
[JsonSerializable(typeof(MonitorConfigurationWrapper.MonitorRectWrapper))]
internal sealed partial class WorkspacesStorageJsonContext : JsonSerializerContext
{
}
