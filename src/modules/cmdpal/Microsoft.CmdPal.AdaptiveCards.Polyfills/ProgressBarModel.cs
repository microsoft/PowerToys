// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>The parsed form of an Adaptive Cards <c>ProgressBar</c> element. Without a value, it's indeterminate.</summary>
internal sealed class ProgressBarModel : IAdaptiveVisualModel
{
    public double? Value { get; init; }

    public double Max { get; init; } = 100;

    public string? Color { get; init; }

    public string IncrementalState { get; init; } = "{}";

    public bool IsIndeterminate => Value is null;

    public double ClampedValue => Math.Clamp(Value ?? 0, 0, Max);

    public static ProgressBarModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            var max = ChartJson.GetNumber(element, "max") ?? 100;
            if (max <= 0)
            {
                warnings.Add("max must be greater than zero.");
                max = 100;
            }

            return new ProgressBarModel
            {
                Value = ChartJson.GetNumber(element, "value"),
                Max = max,
                Color = ChartJson.GetString(element, "color"),
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new ProgressBarModel();
        }
    }
}
