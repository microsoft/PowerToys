// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.TimeDate;

[JsonSerializable(typeof(List<CustomClock>))]
[JsonSerializable(typeof(string))]
internal sealed partial class CustomClockJsonContext : JsonSerializerContext;
