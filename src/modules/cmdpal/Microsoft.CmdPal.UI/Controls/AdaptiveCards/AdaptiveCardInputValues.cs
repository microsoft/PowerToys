// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

/// <summary>Writes input values into Adaptive Card JSON, so a re-rendered card keeps what the user entered.</summary>
internal static class AdaptiveCardInputValues
{
    private const string InputTypePrefix = "Input.";
    private const string NumberInputType = "Input.Number";

    /// <summary>Sets the <c>value</c> of each input in <paramref name="card"/> whose ID is in <paramref name="values"/>.</summary>
    public static void Apply(JsonObject card, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count > 0)
        {
            ApplyToObject(card, values);
        }
    }

    private static void ApplyToValue(IJsonValue value, IReadOnlyDictionary<string, string> values)
    {
        switch (value.ValueType)
        {
            case JsonValueType.Object:
                ApplyToObject(value.GetObject(), values);
                break;
            case JsonValueType.Array:
                foreach (var item in value.GetArray())
                {
                    ApplyToValue(item, values);
                }

                break;
        }
    }

    private static void ApplyToObject(JsonObject element, IReadOnlyDictionary<string, string> values)
    {
        if (TryGetString(element, "type", out var type)
            && type.StartsWith(InputTypePrefix, StringComparison.Ordinal)
            && TryGetString(element, "id", out var id)
            && values.TryGetValue(id, out var currentValue))
        {
            SetValue(element, type, currentValue);
        }

        // Inputs can be nested in containers, columns, and show card actions.
        foreach (var child in element.Values.ToList())
        {
            ApplyToValue(child, values);
        }
    }

    private static void SetValue(JsonObject input, string type, string currentValue)
    {
        if (type != NumberInputType)
        {
            input.SetNamedValue("value", JsonValue.CreateStringValue(currentValue));
            return;
        }

        // A number input's value is a JSON number, and an empty number input has no value.
        if (double.TryParse(currentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            input.SetNamedValue("value", JsonValue.CreateNumberValue(number));
        }
        else
        {
            input.Remove("value");
        }
    }

    private static bool TryGetString(JsonObject element, string name, out string value)
    {
        if (element.TryGetValue(name, out var json) && json.ValueType == JsonValueType.String)
        {
            value = json.GetString();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
