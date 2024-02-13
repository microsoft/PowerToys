// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Awake.Core.Models;

using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AwakeSettings))]
    [JsonSerializable(typeof(SystemPowerCapabilities))]
    internal sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
