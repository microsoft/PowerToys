// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

[JsonSerializable(typeof(RaycastPackageJson))]
[JsonSerializable(typeof(RaycastPackageCommand))]
[JsonSerializable(typeof(GitTreeResponse))]
[JsonSerializable(typeof(GitTreeEntry))]
[JsonSerializable(typeof(GitHubContentResponse))]
[JsonSerializable(typeof(List<GitTreeEntry>))]
[JsonSerializable(typeof(List<RaycastPackageCommand>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class RaycastStoreJsonContext : JsonSerializerContext
{
}
