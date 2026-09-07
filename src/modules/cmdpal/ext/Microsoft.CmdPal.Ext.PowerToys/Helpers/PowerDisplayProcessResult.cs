// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToysExtension.Helpers;

internal sealed class PowerDisplayProcessResult
{
    private PowerDisplayProcessResult(
        int? exitCode,
        string standardOutput,
        string standardError,
        PowerDisplayProcessFailureKind failureKind,
        string errorMessage)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        FailureKind = failureKind;
        ErrorMessage = errorMessage;
    }

    internal int? ExitCode { get; }

    internal string StandardOutput { get; }

    internal string StandardError { get; }

    internal PowerDisplayProcessFailureKind FailureKind { get; }

    internal string ErrorMessage { get; }

    internal static PowerDisplayProcessResult Completed(int exitCode, string standardOutput, string standardError)
        => new(exitCode, standardOutput, standardError, PowerDisplayProcessFailureKind.None, string.Empty);

    internal static PowerDisplayProcessResult Failed(PowerDisplayProcessFailureKind failureKind, string errorMessage)
        => new(null, string.Empty, string.Empty, failureKind, errorMessage);
}
