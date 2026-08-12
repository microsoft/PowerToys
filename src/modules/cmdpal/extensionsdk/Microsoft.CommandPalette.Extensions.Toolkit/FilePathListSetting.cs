// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Specifies which kinds of paths a <see cref="FilePathListSetting"/> can add.
/// </summary>
[Flags]
public enum FilePathListItemType
{
    Files = 1,
    Folders = 2,
    FilesAndFolders = Files | Folders,
}

/// <summary>
/// A setting that manages a list of paths using the Windows file and folder pickers.
/// </summary>
/// <remarks>
/// The value is stored in the settings file and carried on the adaptive card as the same JSON
/// array of objects, <c>[{ "value": "…" }]</c>, so per-item properties can be added later without
/// changing the shape. Readers accept an array of bare strings and ignore properties they do not
/// recognize.
/// </remarks>
public sealed class FilePathListSetting : Setting<IReadOnlyList<string>>
{
    private FilePathListItemType _allowedItemTypes = FilePathListItemType.FilesAndFolders;
    private string _itemValidationPattern = string.Empty;

    /// <summary>
    /// Gets or sets the kinds of paths the user can add.
    /// </summary>
    public FilePathListItemType AllowedItemTypes
    {
        get => _allowedItemTypes;
        set => _allowedItemTypes = ValidateAllowedItemTypes(value);
    }

    /// <summary>
    /// Gets or sets the file extensions shown by the file picker.
    /// Values may use forms such as <c>.txt</c>, <c>*.txt</c>, or <c>*</c>.
    /// </summary>
    public List<string> FileTypeFilter { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional regular expression that every path must match.
    /// The pattern is not anchored, so use <c>^</c> and <c>$</c> to match a whole path.
    /// </summary>
    public string ItemValidationPattern
    {
        get => _itemValidationPattern;
        set => _itemValidationPattern = RegularExpressionPattern.Validate(value, nameof(ItemValidationPattern));
    }

    /// <summary>
    /// Gets or sets the error shown when a path does not match <see cref="ItemValidationPattern"/>.
    /// </summary>
    public string ItemValidationErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether duplicate paths are rejected.
    /// Path comparisons in the host are ordinal and case-insensitive. The default is <see langword="false"/>.
    /// </summary>
    public bool PreventDuplicates { get; set; }

    /// <summary>
    /// Gets or sets the error shown when <see cref="PreventDuplicates"/> is enabled and duplicate paths exist.
    /// </summary>
    public string DuplicateItemErrorMessage { get; set; } = string.Empty;

    private FilePathListSetting()
        : base()
    {
        Value = [];
    }

    public FilePathListSetting(
        string key,
        IReadOnlyList<string> defaultValue,
        FilePathListItemType allowedItemTypes = FilePathListItemType.FilesAndFolders)
        : base(key, defaultValue)
    {
        AllowedItemTypes = allowedItemTypes;
    }

    public FilePathListSetting(
        string key,
        string label,
        string description,
        IReadOnlyList<string> defaultValue,
        FilePathListItemType allowedItemTypes = FilePathListItemType.FilesAndFolders)
        : base(key, label, description, defaultValue)
    {
        AllowedItemTypes = allowedItemTypes;
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.CommandPalette.FilePathList" },
            { "id", Key },
            { "label", string.Empty },
            { "header", Label },
            { "description", Description },
            { "value", StringListSettingUtilities.ToJsonArray(Value) },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "itemValidationPattern", ItemValidationPattern },
            { "itemValidationErrorMessage", ItemValidationErrorMessage },
            { "preventDuplicates", PreventDuplicates },
            { "duplicateItemErrorMessage", DuplicateItemErrorMessage },
            { "fallback", SettingFallback.Notice(Label) },
            { "allowFiles", AllowedItemTypes.HasFlag(FilePathListItemType.Files) },
            { "allowFolders", AllowedItemTypes.HasFlag(FilePathListItemType.Folders) },
            { "fileTypeFilter", FileTypeFilter },
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

    private static FilePathListItemType ValidateAllowedItemTypes(FilePathListItemType allowedItemTypes)
    {
        const FilePathListItemType all = FilePathListItemType.FilesAndFolders;
        if ((allowedItemTypes & all) == 0 || (allowedItemTypes & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allowedItemTypes),
                allowedItemTypes,
                "At least one supported path type must be allowed.");
        }

        return allowedItemTypes;
    }
}
