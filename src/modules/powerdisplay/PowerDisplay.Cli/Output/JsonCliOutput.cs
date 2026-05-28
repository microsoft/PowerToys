// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Machine-readable JSON output. Uses the source-generated
/// <see cref="CliJsonContext"/> so AOT keeps the type metadata.
/// </summary>
public sealed class JsonCliOutput : ICliOutput
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public JsonCliOutput()
        : this(Console.Out, Console.Error)
    {
    }

    public JsonCliOutput(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public void WriteListResult(CliListResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliListResult));

    public void WriteSetResult(CliSetResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliSetResult));

    public void WriteGetResult(CliGetResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliGetResult));

    public void WriteCapabilitiesResult(CliCapabilitiesResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliCapabilitiesResult));

    public void WriteError(CliErrorResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliErrorResult));

    public void WriteWarning(string message) => _stderr.WriteLine(message);
}
