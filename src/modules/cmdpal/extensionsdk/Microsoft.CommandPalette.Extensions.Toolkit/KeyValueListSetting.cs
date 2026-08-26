// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A setting that lets the user add and remove key/value pairs.
/// </summary>
/// <remarks>
/// The value is stored in the settings file as a JSON array of <c>{ "key": …, "value": … }</c>
/// objects, and the adaptive card carries the same array. Duplicate keys are preserved, so the
/// entries are an array rather than an object, and per-item properties can be added later without
/// changing the shape.
/// </remarks>
public sealed class KeyValueListSetting : Setting<IReadOnlyList<KeyValuePair<string, string>>>
{
    private const string KeyPropertyName = "key";
    private const string ValuePropertyName = "value";

    private string _keyValidationPattern = string.Empty;
    private string _valueValidationPattern = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder shown by the key text box.
    /// </summary>
    public string KeyPlaceholder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder shown by the value text box.
    /// </summary>
    public string ValuePlaceholder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the validation error shown when a pair has no key.
    /// </summary>
    public string MissingKeyErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional regular expression that every key must match.
    /// The pattern is not anchored, so use <c>^</c> and <c>$</c> to match a whole key.
    /// </summary>
    public string KeyValidationPattern
    {
        get => _keyValidationPattern;
        set => _keyValidationPattern = RegularExpressionPattern.Validate(value, nameof(KeyValidationPattern));
    }

    /// <summary>
    /// Gets or sets the error shown when a key does not match <see cref="KeyValidationPattern"/>.
    /// </summary>
    public string KeyValidationErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional regular expression that every value must match.
    /// The pattern is not anchored, so use <c>^</c> and <c>$</c> to match a whole value.
    /// </summary>
    public string ValueValidationPattern
    {
        get => _valueValidationPattern;
        set => _valueValidationPattern = RegularExpressionPattern.Validate(value, nameof(ValueValidationPattern));
    }

    /// <summary>
    /// Gets or sets the error shown when a value does not match <see cref="ValueValidationPattern"/>.
    /// </summary>
    public string ValueValidationErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether pairs with duplicate keys are rejected.
    /// Comparisons are ordinal and case-sensitive. The default is <see langword="false"/>.
    /// </summary>
    public bool PreventDuplicateKeys { get; set; }

    /// <summary>
    /// Gets or sets the error shown when <see cref="PreventDuplicateKeys"/> is enabled and duplicate keys exist.
    /// </summary>
    public string DuplicateKeyErrorMessage { get; set; } = string.Empty;

    private KeyValueListSetting()
        : base()
    {
        Value = [];
    }

    public KeyValueListSetting(string key, IReadOnlyList<KeyValuePair<string, string>> defaultValue)
        : base(key, defaultValue)
    {
    }

    public KeyValueListSetting(
        string key,
        string label,
        string description,
        IReadOnlyList<KeyValuePair<string, string>> defaultValue)
        : base(key, label, description, defaultValue)
    {
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.CommandPalette.KeyValueList" },
            { "id", Key },
            { "label", string.Empty },
            { "header", Label },
            { "description", Description },
            { "value", ToJsonArray(Value) },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "keyPlaceholder", KeyPlaceholder },
            { "valuePlaceholder", ValuePlaceholder },
            { "missingKeyErrorMessage", MissingKeyErrorMessage },
            { "keyValidationPattern", KeyValidationPattern },
            { "keyValidationErrorMessage", KeyValidationErrorMessage },
            { "valueValidationPattern", ValueValidationPattern },
            { "valueValidationErrorMessage", ValueValidationErrorMessage },
            { "preventDuplicateKeys", PreventDuplicateKeys },
            { "duplicateKeyErrorMessage", DuplicateKeyErrorMessage },
            { "fallback", SettingFallback.Notice(Label) },
        };
    }

    public override void Update(JsonObject payload)
    {
        if (payload[Key] is not JsonArray array)
        {
            return;
        }

        Value = ParseArray(array);
    }

    public override void UpdateFromForm(JsonObject payload)
    {
        if (payload[Key] is JsonValue value &&
            value.TryGetValue<string>(out var submittedValue) &&
            TryParseJsonArray(submittedValue, out var items))
        {
            Value = items;
        }
    }

    public override string ToState() => $"\"{Key}\": {ToJsonArray(Value)}";

    private static IReadOnlyList<KeyValuePair<string, string>> Normalize(
        IEnumerable<KeyValuePair<string, string>>? items) =>
        items?.Select(static item => new KeyValuePair<string, string>(
            item.Key ?? string.Empty,
            item.Value ?? string.Empty)).ToArray() ?? [];

    private static string ToJsonArray(IEnumerable<KeyValuePair<string, string>>? items) =>
        new JsonArray(Normalize(items)
            .Select(static item => (JsonNode)new JsonObject
            {
                [KeyPropertyName] = item.Key,
                [ValuePropertyName] = item.Value,
            })
            .ToArray())
            .ToJsonString();

    private static bool TryParseJsonArray(
        string? value,
        out IReadOnlyList<KeyValuePair<string, string>> items)
    {
        items = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is not JsonArray array)
        {
            return false;
        }

        items = ParseArray(array);
        return true;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ParseArray(JsonArray array) =>
        array
            .OfType<JsonObject>()
            .Select(static entry => new KeyValuePair<string, string>(
                AsString(entry[KeyPropertyName]),
                AsString(entry[ValuePropertyName])))
            .ToArray();

    private static string AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
}
