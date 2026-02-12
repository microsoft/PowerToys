// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer;

namespace Microsoft.CmdPal.Core.Common.Services.Reports;

public sealed class ErrorReportBuilder : IErrorReportBuilder
{
    private readonly ErrorReportSanitizer _sanitizer = new();
    private readonly IApplicationInfoService _appInfoService;

    private static string Preamble => Properties.Resources.ErrorReport_Global_Preamble;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorReportBuilder"/> class.
    /// </summary>
    /// <param name="appInfoService">Optional application info service. If not provided, a default instance is created.</param>
    public ErrorReportBuilder(IApplicationInfoService? appInfoService = null)
    {
        _appInfoService = appInfoService ?? new ApplicationInfoService(null);
    }

    public string BuildReport(Exception exception, string context, bool redactPii = true)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionMessage = CoalesceExceptionMessage(exception);
        var sanitizedMessage = redactPii ? _sanitizer.Sanitize(exceptionMessage) : exceptionMessage;
        var sanitizedFormattedException = redactPii ? _sanitizer.Sanitize(exception.ToString()) : exception.ToString();

        var applicationInfoSummary = GetAppInfoSafe();
        var applicationInfoSummarySanitized = redactPii ? _sanitizer.Sanitize(applicationInfoSummary) : applicationInfoSummary;

        // Note:
        // - do not localize technical part of the report, we need to ensure it can be read by developers
        // - keep timestamp format should be consistent with the log (makes it easier to search)
        var technicalContent =
               $"""
                ============================================================
                Summary:
                  Message:               {sanitizedMessage}
                  Type:                  {exception.GetType().FullName}
                  Source:                {exception.Source ?? "N/A"}
                  Time:                  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}
                  HRESULT:               0x{exception.HResult:X8} ({exception.HResult})
                  Context:               {context ?? "N/A"}

                {applicationInfoSummarySanitized}

                Stack Trace:
                {exception.StackTrace}

                ------------------ Full Exception Details ------------------
                {sanitizedFormattedException}

                ============================================================
                """;

        return $"""
                {Preamble}
                {technicalContent}
                """;
    }

    private string? GetAppInfoSafe()
    {
        try
        {
            return _appInfoService.GetApplicationInfoSummary();
        }
        catch (Exception ex)
        {
            // Getting application info should never throw, but if it does, we don't want it to prevent the report from being generated
            var message = CoalesceExceptionMessage(ex);
            return $"Failed to get application info summary: {message}";
        }
    }

    private static string CoalesceExceptionMessage(Exception exception)
    {
        // let's try to get a message from the exception or inferred it from the HRESULT
        // to show at least something
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            var temp = Marshal.GetExceptionForHR(exception.HResult)?.Message;
            if (!string.IsNullOrWhiteSpace(temp))
            {
                message = temp + $" (inferred from HRESULT 0x{exception.HResult:X8})";
            }
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "No message available";
        }

        return message;
    }
}
