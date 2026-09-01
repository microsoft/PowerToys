// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using AdaptiveCards.ObjectModel.WinUI3;
using ManagedCommon;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal static class AdaptiveInputValidation
{
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromMilliseconds(250);

    public static string ParsePattern(
        JsonObject inputJson,
        string propertyName,
        string inputType,
        IList<AdaptiveWarning> warnings)
    {
        var pattern = inputJson.GetNamedString(propertyName, string.Empty);
        if (string.IsNullOrEmpty(pattern))
        {
            return string.Empty;
        }

        try
        {
            _ = CreateRegex(pattern);
            return pattern;
        }
        catch (ArgumentException ex)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{inputType}.{propertyName} is not a valid regular expression: {ex.Message}"));
            return string.Empty;
        }
    }

    public static Regex? CreateRegex(string? pattern) =>
        string.IsNullOrEmpty(pattern)
            ? null
            : new Regex(pattern, RegexOptions.CultureInvariant, _matchTimeout);

    public static bool IsMatch(Regex? regex, string value)
    {
        if (regex is null)
        {
            return true;
        }

        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException ex)
        {
            Logger.LogWarning($"Adaptive-card list input validation timed out: {ex.Message}");
            return false;
        }
    }
}
