// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

[JsonSerializable(typeof(GalleryExtensionEntry))]
[JsonSerializable(typeof(GalleryRemoteIndex))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public sealed partial class GallerySerializationContext : JsonSerializerContext
{
}
