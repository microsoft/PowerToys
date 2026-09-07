// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal static class AdaptiveCustomElementJson
{
    public static void ParseCommonProperties(
        IAdaptiveCardElement element,
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        element.AdditionalProperties = JsonObject.Parse(inputJson.Stringify());

        element.Id = inputJson.GetNamedString("id", string.Empty);
        if (string.IsNullOrEmpty(element.Id))
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.RequiredPropertyMissing,
                $"{element.ElementTypeString}.id is required."));
        }

        element.IsVisible = inputJson.GetNamedBoolean("isVisible", true);
        element.Separator = inputJson.GetNamedBoolean("separator", false);
        element.Spacing = ParseEnum(
            inputJson,
            "spacing",
            Spacing.Default,
            element.ElementTypeString,
            warnings);
        element.Height = ParseEnum(
            inputJson,
            "height",
            HeightType.Auto,
            element.ElementTypeString,
            warnings);

        ParseRequirements(element, inputJson, warnings);
        ParseFallback(element, inputJson, elementParsers, actionParsers, warnings);
    }

    public static JsonObject Create(IAdaptiveCardElement element)
    {
        var json = element.AdditionalProperties is null
            ? new JsonObject()
            : JsonObject.Parse(element.AdditionalProperties.Stringify());

        SetString(json, "type", element.ElementTypeString);
        SetString(json, "id", element.Id);
        SetBoolean(json, "isVisible", element.IsVisible);
        SetBoolean(json, "separator", element.Separator);
        SetString(json, "spacing", ToJsonName(element.Spacing));
        SetString(json, "height", ToJsonName(element.Height));

        if (element.FallbackType == FallbackType.Drop)
        {
            SetString(json, "fallback", "drop");
        }
        else if (element.FallbackType == FallbackType.Content && element.FallbackContent is not null)
        {
            try
            {
                json.SetNamedValue("fallback", element.FallbackContent.ToJson());
            }
            catch
            {
                // Keep the original fallback from AdditionalProperties when a nested element cannot serialize.
            }
        }
        else
        {
            json.Remove("fallback");
        }

        return json;
    }

    public static void SetString(JsonObject json, string propertyName, string? value) =>
        json.SetNamedValue(propertyName, JsonValue.CreateStringValue(value ?? string.Empty));

    public static void SetBoolean(JsonObject json, string propertyName, bool value) =>
        json.SetNamedValue(propertyName, JsonValue.CreateBooleanValue(value));

    public static void SetStringArray(JsonObject json, string propertyName, IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(JsonValue.CreateStringValue(value));
        }

        json.SetNamedValue(propertyName, array);
    }

    private static TEnum ParseEnum<TEnum>(
        JsonObject inputJson,
        string propertyName,
        TEnum defaultValue,
        string elementType,
        IList<AdaptiveWarning> warnings)
        where TEnum : struct, Enum
    {
        var value = inputJson.GetNamedString(propertyName, string.Empty);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsedValue))
        {
            return parsedValue;
        }

        warnings.Add(new AdaptiveWarning(
            WarningStatusCode.UnknownEnumValue,
            $"{elementType}.{propertyName} has unknown value '{value}'."));
        return defaultValue;
    }

    /// <summary>
    /// Reads the <c>requires</c> map so the renderer can fall back when the host does not provide
    /// a feature the card asks for.
    /// </summary>
    private static void ParseRequirements(
        IAdaptiveCardElement element,
        JsonObject inputJson,
        IList<AdaptiveWarning> warnings)
    {
        if (!inputJson.TryGetValue("requires", out var requires))
        {
            return;
        }

        if (requires.ValueType != JsonValueType.Object)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{element.ElementTypeString}.requires must be an object."));
            return;
        }

        foreach (var requirement in requires.GetObject())
        {
            if (requirement.Value.ValueType != JsonValueType.String)
            {
                warnings.Add(new AdaptiveWarning(
                    WarningStatusCode.InvalidValue,
                    $"{element.ElementTypeString}.requires.{requirement.Key} must be a version string."));
                continue;
            }

            element.Requirements.Add(new AdaptiveRequirement(requirement.Key, requirement.Value.GetString()));
        }
    }

    private static void ParseFallback(
        IAdaptiveCardElement element,
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        if (!inputJson.TryGetValue("fallback", out var fallback))
        {
            return;
        }

        if (fallback.ValueType == JsonValueType.String)
        {
            if (string.Equals(fallback.GetString(), "drop", StringComparison.OrdinalIgnoreCase))
            {
                element.FallbackType = FallbackType.Drop;
            }
            else
            {
                warnings.Add(new AdaptiveWarning(
                    WarningStatusCode.InvalidValue,
                    $"{element.ElementTypeString}.fallback must be 'drop' or an Adaptive Card element."));
            }

            return;
        }

        if (fallback.ValueType != JsonValueType.Object)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{element.ElementTypeString}.fallback must be 'drop' or an Adaptive Card element."));
            return;
        }

        var fallbackJson = fallback.GetObject();
        var fallbackType = fallbackJson.GetNamedString("type", string.Empty);
        if (string.IsNullOrEmpty(fallbackType))
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.RequiredPropertyMissing,
                $"{element.ElementTypeString}.fallback.type is required."));
            return;
        }

        IAdaptiveElementParser? parser;
        try
        {
            parser = elementParsers.Get(fallbackType);
        }
        catch
        {
            parser = null;
        }

        if (parser is null)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.UnknownElementType,
                $"{element.ElementTypeString}.fallback has unknown element type '{fallbackType}'."));
            return;
        }

        element.FallbackContent = parser.FromJson(fallbackJson, elementParsers, actionParsers, warnings);
        element.FallbackType = FallbackType.Content;
    }

    private static string ToJsonName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0 ? string.Empty : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
