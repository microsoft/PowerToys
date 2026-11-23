// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class RecoveryOutcome
{
    internal const string LostClipboardError = "Original clipboard was held only in the exited worker; restore the clean VM baseline. No clipboard data was persisted or guessed.";

    internal static void Require(JsonObject result, string runId, bool clipboardLost)
    {
        var errors = result["Errors"] as JsonArray ?? throw new InvalidDataException("Missing recovery errors.");
        if (result["FormatVersion"]?.GetValue<int>() != 1 ||
            result["RunId"]?.GetValue<string>() != runId ||
            result["Status"]?.GetValue<string>() != (clipboardLost ? "Incomplete" : "Recovered") ||
            result["SettingsRestored"]?.GetValue<bool>() != true ||
            result["ClipboardRestored"]?.GetValue<bool>() != !clipboardLost ||
            result["RequiresBaselineReset"]?.GetValue<bool>() != clipboardLost ||
            errors.Count != (clipboardLost ? 1 : 0) ||
            (clipboardLost && errors[0]?.GetValue<string>() != LostClipboardError))
        {
            throw new InvalidDataException("Recovery did not produce the exact expected safe verdict.");
        }
    }

    internal static DateTime ProcessTimestamp(JsonNode value)
    {
        if (value is JsonValue json && json.TryGetValue<DateTime>(out var timestamp) && timestamp.Kind == DateTimeKind.Utc)
        {
            return timestamp;
        }

        var text = value.GetValue<string>();
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp) && timestamp.Kind == DateTimeKind.Utc)
        {
            return timestamp;
        }

        if (text.StartsWith("/Date(", StringComparison.Ordinal) && text.EndsWith(")/", StringComparison.Ordinal) &&
            long.TryParse(text.AsSpan(6, text.Length - 8), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        }

        throw new InvalidDataException("Process birth time must be an explicit UTC ISO or Windows PowerShell timestamp.");
    }
}
