// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Interface for function data related to settings.
/// </summary>
public interface ISettingsFunctionData
{
    /// <summary>
    /// Gets the input settings resource object.
    /// </summary>
    public ISettingsResourceObject Input { get; }

    /// <summary>
    /// Gets the output settings resource object.
    /// </summary>
    public ISettingsResourceObject Output { get; }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    public void GetState();

    /// <summary>
    /// Sets the current settings.
    /// </summary>
    public void SetState();

    /// <summary>
    /// Tests if the current settings and the desired state are valid.
    /// </summary>
    /// <returns>True if the current settings match the desired state; otherwise false.</returns>
    public bool TestState();

    /// <summary>
    /// Gets the difference between the current settings and the desired state in JSON format.
    /// </summary>
    /// <returns>A JSON array representing the differences.</returns>
    public JsonArray GetDiffJson();

    /// <summary>
    /// Gets the schema for the settings resource object.
    /// </summary>
    /// <returns></returns>
    public string Schema();
}
