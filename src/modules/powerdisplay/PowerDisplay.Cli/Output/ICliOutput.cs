// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Abstraction over text vs JSON output. Each command builds the typed result
/// record and hands it to one of these methods. Errors are routed through
/// <see cref="WriteError"/> regardless of which command produced them.
/// </summary>
public interface ICliOutput
{
    void WriteListResult(CliListResult result);

    void WriteSetResult(CliSetResult result);

    void WriteGetResult(CliGetResult result);

    void WriteCapabilitiesResult(CliCapabilitiesResult result);

    void WriteProfileListResult(CliProfileListResult result);

    void WriteApplyProfileResult(CliApplyProfileResult result);

    void WriteError(CliErrorResult result);

    void WriteWarning(string message);
}
