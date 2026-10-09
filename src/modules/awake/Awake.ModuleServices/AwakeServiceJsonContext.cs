// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.ModuleServices;

[JsonSerializable(typeof(AwakeSettings))]
internal sealed partial class AwakeServiceJsonContext : JsonSerializerContext
{
}
