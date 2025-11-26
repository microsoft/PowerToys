// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspacesData))]
[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspaceProject))]
[JsonSerializable(typeof(WorkspaceItemsHelper.WorkspaceApplication))]
[JsonSerializable(typeof(AwakeSettingsDocument))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
internal sealed partial class PowerToysJsonContext : JsonSerializerContext
{
}
