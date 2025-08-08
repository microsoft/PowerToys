// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace PowerToys.DSC.Models;

internal interface ISettingsResourceObject
{
    public ISettingsConfig GetSettings();

    public void SetSettings(ISettingsConfig settings);

    public void SetInDesiredState(bool inDesiredState);

    public JsonNode ToJson();
}
