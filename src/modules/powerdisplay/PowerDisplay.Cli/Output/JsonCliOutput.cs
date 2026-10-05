// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Machine-readable JSON Lines output. Each result is one compact JSON object; successful results
/// go to stdout and errors go to stderr. Errors retain their stable structured fields while also
/// materializing the CLI-localized message and hint. Warnings are omitted so both streams remain
/// JSON-only.
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
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliListResult));

    public void WriteSetResult(CliSetResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliSetResult));

    public void WriteGetResult(CliGetResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliGetResult));

    public void WriteCapabilitiesResult(CliCapabilitiesResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliCapabilitiesResult));

    public void WriteProfileListResult(CliProfileListResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliProfileListResult));

    public void WriteApplyProfileResult(CliApplyProfileResult result)
        => _stdout.WriteLine(JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliApplyProfileResult));

    public void WriteError(CliErrorResult result)
    {
        var source = result.Error;
        var (message, hint) = CliErrorLocalizer.Localize(source);

        // IPC errors normally carry only MessageId plus structured data. Serialize a localized copy
        // so JSON consumers receive displayable prose without losing stable fields or mutating the
        // app-produced envelope that may still be observed by the dispatcher/caller.
        var localized = new CliErrorResult
        {
            IsError = result.IsError,
            Version = result.Version,
            Command = result.Command,
            Monitor = result.Monitor,
            Error = new CliError
            {
                Code = source.Code,
                MessageId = source.MessageId,
                Message = message,
                Setting = source.Setting,
                Value = source.Value,
                ExpectedRange = source.ExpectedRange,
                Supported = source.Supported,
                Detail = source.Detail,
                Hint = hint,
            },
        };

        _stderr.WriteLine(JsonSerializer.Serialize(localized, ContractsJsonContext.Default.CliErrorResult));
    }

    public void WriteWarning(string message)
    {
    }
}
