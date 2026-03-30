// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

[JsonSerializable(typeof(List<string>), TypeInfoPropertyName = "GalleryIndexIds")]
[JsonSerializable(typeof(List<GalleryIndexEntry>), TypeInfoPropertyName = "GalleryIndexEntries")]
[JsonSerializable(typeof(GalleryExtensionEntry))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public sealed partial class GallerySerializationContext : JsonSerializerContext
{
}
