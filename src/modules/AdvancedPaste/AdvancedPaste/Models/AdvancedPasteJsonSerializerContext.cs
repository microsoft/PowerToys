// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace AdvancedPaste.Models
{
    [JsonSerializable(typeof(CustomQuery))]
    public partial class AdvancedPasteJsonSerializerContext : JsonSerializerContext
    {
    }
}
