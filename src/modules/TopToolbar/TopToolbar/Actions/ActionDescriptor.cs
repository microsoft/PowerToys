// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TopToolbar.Actions
{
    public sealed class ActionDescriptor
    {
        public string Id { get; set; } = string.Empty;

        public string ProviderId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public ActionKind Kind { get; set; } = ActionKind.Command;

        public ActionIcon Icon { get; set; } = new ActionIcon();

        public string GroupHint { get; set; } = string.Empty;

        public double? Order { get; set; }

        public IList<string> Keywords { get; } = new List<string>();

        public JsonNode ArgsSchema { get; set; }

        public JsonNode Preview { get; set; }

        public bool? CanExecute { get; set; }
    }
}
