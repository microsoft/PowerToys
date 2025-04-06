// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal interface ISettingsForm
{
    public string ToForm();

    public void Update(JsonObject payload);

    public Dictionary<string, object> ToDictionary();

    public string ToDataIdentifier();

    public string ToState();
}
