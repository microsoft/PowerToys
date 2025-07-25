// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public interface ISettingsForm
{
    string ToForm();

    void Update(JsonObject payload);

    Dictionary<string, object> ToDictionary();

    string ToDataIdentifier();

    string ToState();
}
