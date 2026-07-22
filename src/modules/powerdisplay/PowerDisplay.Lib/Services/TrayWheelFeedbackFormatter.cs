// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Formats tray wheel adjustment feedback without UI or shell dependencies.
/// </summary>
public static class TrayWheelFeedbackFormatter
{
    private const int ExactValueLimit = 4;
    private const string NeutralPrimaryFormat = "Primary display · {0}";
    private const string NeutralPrimaryPluralFormat = "Primary displays · {0}";
    private const string NeutralAllFormat = "All displays · {0}";
    private const string NeutralPercentageFormat = "{0}%";
    private const string NeutralRangeFormat = "{0}–{1} ({2} displays)";
    private const string NeutralListSeparator = ", ";

    /// <summary>
    /// Formats one feedback payload.
    /// </summary>
    public static string? Format(
        TrayWheelAdjustmentFeedback feedback,
        TrayWheelFeedbackTemplates templates,
        CultureInfo culture,
        int maxLength = 127)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (feedback.BrightnessValues is null || feedback.BrightnessValues.Count == 0)
        {
            return null;
        }

        var mode = feedback.Mode.Normalize();
        if (mode == MouseWheelControlMode.Disabled)
        {
            return null;
        }

        var count = feedback.BrightnessValues.Count;
        var percentages = new string[count];
        var minimum = 100;
        var maximum = 0;

        for (var i = 0; i < count; i++)
        {
            var value = Math.Clamp(feedback.BrightnessValues[i], 0, 100);
            percentages[i] = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                value);
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        string valuesText;
        if (count <= ExactValueLimit)
        {
            valuesText = string.Join(
                string.IsNullOrEmpty(templates.ListSeparator)
                    ? NeutralListSeparator
                    : templates.ListSeparator,
                percentages);
        }
        else
        {
            var minimumText = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                minimum);
            var maximumText = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                maximum);
            valuesText = SafeFormat(
                templates.RangeFormat,
                NeutralRangeFormat,
                culture,
                minimumText,
                maximumText,
                count);
        }

        var result = mode == MouseWheelControlMode.PrimaryDisplay
            ? count == 1
                ? SafeFormat(templates.PrimaryFormat, NeutralPrimaryFormat, culture, valuesText)
                : SafeFormat(
                    templates.PrimaryPluralFormat,
                    NeutralPrimaryPluralFormat,
                    culture,
                    valuesText)
            : SafeFormat(templates.AllFormat, NeutralAllFormat, culture, valuesText);

        return LimitUtf16(result, maxLength);
    }

    private static string SafeFormat(
        string? localized,
        string neutral,
        CultureInfo culture,
        params object[] arguments)
    {
        if (!string.IsNullOrEmpty(localized))
        {
            try
            {
                return string.Format(culture, localized, arguments);
            }
            catch (FormatException)
            {
            }
        }

        return string.Format(CultureInfo.InvariantCulture, neutral, arguments);
    }

    private static string LimitUtf16(string value, int maxLength)
    {
        if (value.Length == 0)
        {
            return value;
        }

        if (value.Length <= maxLength)
        {
            return char.IsHighSurrogate(value[^1]) ? value[..^1] : value;
        }

        var length = maxLength;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }
}
