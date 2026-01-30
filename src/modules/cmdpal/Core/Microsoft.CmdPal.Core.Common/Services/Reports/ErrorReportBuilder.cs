// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Core.Common.Services.Reports;

public sealed class ErrorReportBuilder : IErrorReportBuilder
{
    private readonly ErrorReportSanitizer _sanitizer = new();

    private static string Preamble => Properties.Resources.ErrorReport_Global_Preamble;

    public string BuildReport(Exception exception, string context, bool redactPii = true)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionMessage = CoalesceExceptionMessage(exception);
        var sanitizedMessage = redactPii ? _sanitizer.Sanitize(exceptionMessage) : exceptionMessage;
        var sanitizedFormattedException = redactPii ? _sanitizer.Sanitize(exception.ToString()) : exception.ToString();

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

                Application:
                  App version:           {GetAppVersionSafe()}
                  Is elevated:           {GetElevationStatus()}

                Environment:
                  OS version:            {RuntimeInformation.OSDescription}
                  OS architecture:       {RuntimeInformation.OSArchitecture}
                  Runtime identifier:    {RuntimeInformation.RuntimeIdentifier}
                  Framework:             {RuntimeInformation.FrameworkDescription}
                  Process architecture:  {RuntimeInformation.ProcessArchitecture}
                  Culture:               {CultureInfo.CurrentCulture.Name}
                  UI culture:            {CultureInfo.CurrentUICulture.Name}

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

    private static string GetElevationStatus()
    {
        // Note: do not localize technical part of the report, we need to ensure it can be read by developers
        try
        {
            var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            return isElevated ? "yes" : "no";
        }
        catch (Exception)
        {
            return "Failed to determine elevation status";
        }
    }

    private static string GetAppVersionSafe()
    {
        // Note: do not localize technical part of the report, we need to ensure it can be read by developers
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch (Exception)
        {
            return "Failed to retrieve app version";
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
