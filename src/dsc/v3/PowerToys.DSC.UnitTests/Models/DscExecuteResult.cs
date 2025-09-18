// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.UnitTests.Models;

/// <summary>
/// Result of executing a DSC command.
/// </summary>
public class DscExecuteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DscExecuteResult"/> class.
    /// </summary>
    /// <param name="success">Value indicating whether the command execution was successful.</param>
    /// <param name="output">Output stream content.</param>
    /// <param name="error">Error stream content.</param>
    public DscExecuteResult(bool success, string output, string error)
    {
        Success = success;
        Output = output;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the command execution was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the output stream content of the operation.
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Gets the error stream content of the operation.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the messages from the error stream.
    /// </summary>
    /// <returns>List of messages with their levels.</returns>
    public List<(DscMessageLevel Level, string Message)> Messages()
    {
        var lines = Error.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        return lines.SelectMany(line =>
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(line);
            return map.Select(v => (GetMessageLevel(v.Key), v.Value)).ToList();
        }).ToList();
    }

    /// <summary>
    /// Gets the output as state.
    /// </summary>
    /// <returns>State.</returns>
    public T OutputState<T>()
    {
        var lines = Output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        Debug.Assert(lines.Length == 1, "Output should contain exactly one line.");
        return JsonSerializer.Deserialize<T>(lines[0]);
    }

    /// <summary>
    /// Gets the output as state and diff.
    /// </summary>
    /// <returns>State and diff.</returns>
    public (T State, List<string> Diff) OutputStateAndDiff<T>()
    {
        var lines = Output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        Debug.Assert(lines.Length == 2, "Output should contain exactly two lines.");
        var obj = JsonSerializer.Deserialize<T>(lines[0]);
        var diff = JsonSerializer.Deserialize<List<string>>(lines[1]);
        return (obj, diff);
    }

    /// <summary>
    /// Gets the message level from a string representation.
    /// </summary>
    /// <param name="level">The string representation of the message level.</param>
    /// <returns>The level as <see cref="DscMessageLevel"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the level is unknown.</exception>
    private DscMessageLevel GetMessageLevel(string level)
    {
        return level switch
        {
            "error" => DscMessageLevel.Error,
            "warn" => DscMessageLevel.Warning,
            "info" => DscMessageLevel.Info,
            "debug" => DscMessageLevel.Debug,
            "trace" => DscMessageLevel.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown message level"),
        };
    }
}
