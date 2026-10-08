// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A setting that lets the user add and remove strings from a list.
/// </summary>
/// <remarks>
/// The value is stored in the settings file and carried on the adaptive card as the same JSON
/// array of objects, <c>[{ "value": "…" }]</c>, so per-item properties can be added later without
/// changing the shape. Readers accept an array of bare strings and ignore properties they do not
/// recognize.
/// </remarks>
public sealed class StringListSetting : Setting<IReadOnlyList<string>>
{
    private string _itemValidationPattern = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder shown by the add-item text box.
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional regular expression that every item must match.
    /// The pattern is not anchored, so use <c>^</c> and <c>$</c> to match a whole item.
    /// </summary>
    public string ItemValidationPattern
    {
        get => _itemValidationPattern;
        set => _itemValidationPattern = RegularExpressionPattern.Validate(value, nameof(ItemValidationPattern));
    }

    /// <summary>
    /// Gets or sets the error shown when an item does not match <see cref="ItemValidationPattern"/>.
    /// </summary>
    public string ItemValidationErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether duplicate items are rejected.
    /// Comparisons are ordinal and case-sensitive. The default is <see langword="false"/>.
    /// </summary>
    public bool PreventDuplicates { get; set; }

    /// <summary>
    /// Gets or sets the error shown when <see cref="PreventDuplicates"/> is enabled and duplicate items exist.
    /// </summary>
    public string DuplicateItemErrorMessage { get; set; } = string.Empty;

    private StringListSetting()
        : base()
    {
        Value = [];
    }

    public StringListSetting(string key, IReadOnlyList<string> defaultValue)
        : base(key, defaultValue)
    {
    }

    public StringListSetting(string key, string label, string description, IReadOnlyList<string> defaultValue)
        : base(key, label, description, defaultValue)
    {
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.CommandPalette.StringList" },
            { "id", Key },
            { "label", string.Empty },
            { "header", Label },
            { "description", Description },
            { "value", StringListSettingUtilities.ToJsonArray(Value) },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "placeholder", Placeholder },
            { "itemValidationPattern", ItemValidationPattern },
            { "itemValidationErrorMessage", ItemValidationErrorMessage },
            { "preventDuplicates", PreventDuplicates },
            { "duplicateItemErrorMessage", DuplicateItemErrorMessage },
            { "fallback", SettingFallback.Notice(Label) },
        };
    }

    public override void Update(JsonObject payload)
    {
        if (payload[Key] is JsonArray array)
        {
            Value = StringListSettingUtilities.ParseArray(array);
        }
    }

    public override void UpdateFromForm(JsonObject payload)
    {
        if (payload[Key] is JsonValue value &&
            value.TryGetValue<string>(out var submittedValue) &&
            StringListSettingUtilities.TryParseJsonArray(submittedValue, out var items))
        {
            Value = items;
        }
    }

    public override string ToState() => StringListSettingUtilities.ToPersistedValue(Key, Value);
}
