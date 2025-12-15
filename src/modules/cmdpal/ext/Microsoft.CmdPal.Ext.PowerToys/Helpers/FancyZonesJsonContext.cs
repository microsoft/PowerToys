// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

[JsonSerializable(typeof(FancyZonesAppliedLayoutsFile))]
[JsonSerializable(typeof(FancyZonesLayoutTemplatesFile))]
[JsonSerializable(typeof(FancyZonesCustomLayoutsFile))]
[JsonSerializable(typeof(FancyZonesEditorParametersFile))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class FancyZonesJsonContext : JsonSerializerContext
{
}
