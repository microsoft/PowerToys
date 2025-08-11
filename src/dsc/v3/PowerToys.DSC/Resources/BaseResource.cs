// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.Resources;

internal abstract class BaseResource
{
    public string? Module { get; }

    public BaseResource(string? module)
    {
        Module = module;
    }

    public abstract bool Get(string? input);

    public abstract bool Set(string? input);

    public abstract bool Test(string? input);

    public abstract bool Export(string? input);

    public abstract bool Schema();

    public abstract bool Manifest(string? outputDir);

    public abstract IList<string> GetSupportedModules();

    protected void WriteJsonOutputLine(JsonNode output)
    {
        var json = output.ToJsonString(new() { WriteIndented = false });
        WriteJsonOutputLine(json);
    }

    protected void WriteJsonOutputLine(string output)
    {
        Console.WriteLine(output);
    }

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
    /// Gets the message level as a string based on the provided DscMessageLevel enum value.
    /// </summary>
    /// <param name="level">The DscMessageLevel enum value.</param>
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
