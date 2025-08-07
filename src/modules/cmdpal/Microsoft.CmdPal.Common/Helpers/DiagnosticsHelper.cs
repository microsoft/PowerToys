// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Provides utility methods for building diagnostic and error messages.
/// </summary>
public static class DiagnosticsHelper
{
    /// <summary>
    /// Builds a comprehensive exception message with timestamp and detailed diagnostic information.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="extensionHint">A hint about which extension caused the exception to help with debugging.</param>
    /// <returns>A string containing the exception details, timestamp, and source information for diagnostic purposes.</returns>
    public static string BuildExceptionMessage(Exception exception, string? extensionHint)
    {
        var locationHint = string.IsNullOrWhiteSpace(extensionHint) ? "application" : $"'{extensionHint}' extension";

        // note: keep date time kind and format consistent with the log
        return $"""
                ============================================================
                😢 An unexpected error occurred in the {locationHint}.

                Summary:
                  Message:    {(string.IsNullOrWhiteSpace(exception.Message) ? "[No message available]" : exception.Message )}
                  Type:       {exception.GetType().FullName}
                  Source:     {exception.Source ?? "N/A"}
                  Time:       {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fffffff}
                  HRESULT:    0x{exception.HResult:X8} ({exception.HResult})

                Stack Trace:
                {exception.StackTrace ?? "[No stack trace available]"}

                ------------------ Full Exception Details ------------------
                {exception}

                ℹ️ If you need further assistance, please include this information in your support request.
                ============================================================
                
                """;
    }
}
