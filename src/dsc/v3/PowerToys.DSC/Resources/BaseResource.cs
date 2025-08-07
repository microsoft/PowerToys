// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.Resources;

internal abstract class BaseResource
{
    public string ModuleName { get; }

    public BaseResource(string? moduleName)
    {
        ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName), "Module name cannot be null.");
    }

    public abstract bool Get();

    public abstract bool Set();

    public abstract bool Test();

    public abstract bool Export();

    public abstract void Schema();

    public abstract void Manifest();

    protected void WriteJsonOutputLine<T>(T obj)
    {
        var settingsJson = JsonSerializer.Serialize(obj);
        Console.WriteLine(settingsJson);
    }

    protected void WriteMessageOutputLine(DscMessageLevel level, string message)
    {
        var messageObj = new Dictionary<string, string>
        {
            [GetMessageLevel(level)] = message,
        };
        var messageJson = JsonSerializer.Serialize(messageObj);
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
