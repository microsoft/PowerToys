// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.DSCResources;

/// <summary>
/// Base class for all DSC resources.
/// </summary>
public abstract class BaseResource
{
    /// <summary>
    /// Gets the name of the resource.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the module being used by the resource, if provided.
    /// </summary>
    public string? Module { get; }

    public BaseResource(string name, string? module)
    {
        Name = name;
        Module = module;
    }

    /// <summary>
    /// Calls the get method on the resource.
    /// </summary>
    /// <param name="input">The input string, if any.</param>
    /// <returns>True if the operation was successful; otherwise false.</returns>
    public abstract bool GetState(string? input);

    /// <summary>
    /// Calls the set method on the resource.
    /// </summary>
    /// <param name="input">The input string, if any.</param>
    /// <returns>True if the operation was successful; otherwise false.</returns>
    public abstract bool SetState(string? input);

    /// <summary>
    /// Calls the test method on the resource.
    /// </summary>
    /// <param name="input">The input string, if any.</param>
    /// <returns>True if the operation was successful; otherwise false.</returns>
    public abstract bool TestState(string? input);

    /// <summary>
    /// Calls the export method on the resource.
    /// </summary>
    /// <param name="input"> The input string, if any.</param>
    /// <returns>True if the operation was successful; otherwise false.</returns>
    public abstract bool ExportState(string? input);

    /// <summary>
    /// Calls the schema method on the resource.
    /// </summary>
    /// <returns>True if the operation was successful; otherwise false.</returns>
    public abstract bool Schema();

    /// <summary>
    /// Generates a DSC resource JSON manifest for the resource. If the
    /// outputDir is not provided, the manifest will be printed to the console.
    /// </summary>
    /// <param name="outputDir"> The directory where the manifest should be
    /// saved. If null, the manifest will be printed to the console.</param>
    /// <returns>True if the manifest was successfully generated and saved,otherwise false.</returns>
    public abstract bool Manifest(string? outputDir);

    /// <summary>
    /// Gets the list of supported modules for the resource.
    /// </summary>
    /// <returns>Gets a list of supported modules.</returns>
    public abstract IList<string> GetSupportedModules();

    /// <summary>
    /// Writes a JSON output line to the console.
    /// </summary>
    /// <param name="output">The JSON output to write.</param>
    protected void WriteJsonOutputLine(JsonNode output)
    {
        var json = output.ToJsonString(new() { WriteIndented = false });
        WriteJsonOutputLine(json);
    }

    /// <summary>
    /// Writes a JSON output line to the console.
    /// </summary>
    /// <param name="output">The JSON output to write.</param>
    protected void WriteJsonOutputLine(string output)
    {
        Console.WriteLine(output);
    }

    /// <summary>
    /// Writes a message output line to the console with the specified message level.
    /// </summary>
    /// <param name="level">The level of the message.</param>
    /// <param name="message">The message to write.</param>
    protected void WriteMessageOutputLine(DscMessageLevel level, string message)
    {
        var messageObj = new Dictionary<string, string>
        {
            [GetMessageLevel(level)] = message,
        };
        var messageJson = System.Text.Json.JsonSerializer.Serialize(messageObj);
        Console.Error.WriteLine(messageJson);
    }

    /// <summary>
    /// Gets the message level as a string based on the provided dsc message level enum value.
    /// </summary>
    /// <param name="level">The dsc message level.</param>
    /// <returns>A string representation of the message level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided message level is not recognized.</exception>
    private static string GetMessageLevel(DscMessageLevel level)
    {
        return level switch
        {
            DscMessageLevel.Error => "error",
            DscMessageLevel.Warning => "warn",
            DscMessageLevel.Info => "info",
            DscMessageLevel.Debug => "debug",
            DscMessageLevel.Trace => "trace",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
    }
}
