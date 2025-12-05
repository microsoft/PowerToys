// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace WorkspacesCsharpLibrary.Data;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WorkspacesStorage.WorkspacesFile))]
internal sealed partial class WorkspacesStorageJsonContext : JsonSerializerContext
{
}
