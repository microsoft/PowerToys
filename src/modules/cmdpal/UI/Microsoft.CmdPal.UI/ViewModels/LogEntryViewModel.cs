// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class LogEntryViewModel : ObservableObject
{
    private const int HeaderMaxLength = 80;
    private const string WarningGlyph = "\uE7BA";
    private const string ErrorGlyph = "\uEA39";
    private const string TimestampFormat = "HH:mm:ss";

    private DateTimeOffset Timestamp { get; }

    private string Severity { get; }

    private string Message { get; }

    private string FormattedTimestamp { get; }

    public string SeverityGlyph { get; }

    [ObservableProperty]
    public partial string Header { get; private set; }

    [ObservableProperty]
    public partial string Description { get; private set; }

    [ObservableProperty]
    public partial string Details { get; private set; }

    public LogEntryViewModel(DateTimeOffset timestamp, string severity, string message, string details)
    {
        Timestamp = timestamp;
        Severity = severity;
        Message = message;
        Details = details;

        SeverityGlyph = severity.ToUpperInvariant() switch
        {
            "WARNING" => WarningGlyph,
            "ERROR" => ErrorGlyph,
            _ => string.Empty,
        };

        FormattedTimestamp = timestamp.ToString(TimestampFormat, CultureInfo.CurrentCulture);
        Description = $"{FormattedTimestamp} • {Message}";
        Header = Message;
    }

    public void AppendDetails(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Details += Environment.NewLine + message;

        // Make header the second line of details (because that's actually the message itself):
        var detailsLines = Details.Split([Environment.NewLine], StringSplitOptions.None);
        if (detailsLines.Length < 2)
        {
            return;
        }

        Header = detailsLines[1].Trim();
        if (Header.Length > HeaderMaxLength)
        {
            Header = Header[..(HeaderMaxLength - 1)] + "…";
        }
    }
}
