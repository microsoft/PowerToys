// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.Reports;

/// <summary>
/// Defines a contract for creating human-readable error reports from exceptions,
/// suitable for logs, telemetry, or user-facing diagnostics.
/// </summary>
/// <remarks>
/// Implementations should ensure reports are consistent and optionally redact
/// personally identifiable or sensitive information when requested.
/// </remarks>
public interface IErrorReportBuilder
{
    /// <summary>
    /// Builds a formatted error report for the specified <paramref name="exception"/> and <paramref name="context"/>.
    /// </summary>
    /// <param name="exception">The exception that triggered the error report.</param>
    /// <param name="context">
    /// A short, human-readable description of where or what was being executed when the error occurred
    /// (e.g., the operation name, component, or scenario).
    /// </param>
    /// <param name="redactPii">
    /// When true, attempts to remove or obfuscate personally identifiable or sensitive information
    /// (such as file paths, emails, machine/usernames, tokens). Defaults to true.
    /// </param>
    /// <returns>
    /// A formatted string containing the error report, suitable for logging or telemetry submission.
    /// </returns>
    string BuildReport(Exception exception, string context, bool redactPii = true);
}
