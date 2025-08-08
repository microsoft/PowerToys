// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace PowerToys.DSC.Models;

internal interface ISettingsFunctionData
{
    public void Get();

    public void Set();

    public bool Test();

    public JsonArray DiffJson();

    public string Schema();

    public ISettingsResourceObject GetInput();

    public ISettingsResourceObject GetOutput();
}
