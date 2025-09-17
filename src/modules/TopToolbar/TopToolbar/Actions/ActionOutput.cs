// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace TopToolbar.Actions
{
    public sealed class ActionOutput
    {
        public string Type { get; set; } = "text";

        public JsonElement Data { get; set; }
    }
}
