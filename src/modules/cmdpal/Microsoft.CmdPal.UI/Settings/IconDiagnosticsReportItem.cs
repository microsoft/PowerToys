// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.UI.Helpers;

namespace Microsoft.CmdPal.UI.Settings;

internal sealed class IconDiagnosticsReportItem
{
    public IconLoadDiagnosticsReport Report { get; }

    public string Header => $"Session {Report.SessionId}";

    public string Description { get; }

    public string CopyAutomationId => $"CmdPal_InternalPage_CopyIconDiagnostics_{Report.SessionId}";

    public IconDiagnosticsReportItem(IconLoadDiagnosticsReport report)
    {
        Report = report;

        var startedLocal = report.StartedUtc.ToLocalTime();
        var endedLocal = report.EndedUtc.ToLocalTime();
        Description = string.Format(
            CultureInfo.CurrentCulture,
            "Started: {0:G}  •  Ended: {1:G}  •  Duration: {2}",
            startedLocal,
            endedLocal,
            FormatDuration(report.Duration));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalDays >= 1
            ? duration.ToString("d'.'hh':'mm':'ss'.'fff", CultureInfo.InvariantCulture)
            : duration.ToString("hh':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
    }
}
