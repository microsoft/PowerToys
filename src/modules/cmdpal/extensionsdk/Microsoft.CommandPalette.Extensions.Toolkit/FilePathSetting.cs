// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Specifies whether a <see cref="FilePathSetting"/> selects a file or a folder.
/// </summary>
public enum FilePathSelectionMode
{
    File,
    Folder,
}

/// <summary>
/// A setting that lets the user enter or browse for one file or folder path.
/// </summary>
public sealed class FilePathSetting : Setting<string>
{
    private FilePathSelectionMode _selectionMode;
    private string _validationPattern = string.Empty;

    /// <summary>
    /// Gets or sets whether the picker selects a file or folder.
    /// </summary>
    public FilePathSelectionMode SelectionMode
    {
        get => _selectionMode;
        set => _selectionMode = ValidateSelectionMode(value);
    }

    /// <summary>
    /// Gets or sets the file extensions shown by the file picker.
    /// Values may use forms such as <c>.txt</c>, <c>*.txt</c>, or <c>*</c>.
    /// This property is ignored when <see cref="SelectionMode"/> is <see cref="FilePathSelectionMode.Folder"/>.
    /// </summary>
    public List<string> FileTypeFilter { get; set; } = [];

    /// <summary>
    /// Gets or sets the placeholder shown by the path text box.
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional regular expression that the path must match.
    /// </summary>
    public string ValidationPattern
    {
        get => _validationPattern;
        set => _validationPattern = RegularExpressionPattern.Validate(value, nameof(ValidationPattern));
    }

    /// <summary>
    /// Gets or sets the error shown when the path does not match <see cref="ValidationPattern"/>.
    /// </summary>
    public string ValidationErrorMessage { get; set; } = string.Empty;

    private FilePathSetting()
        : base()
    {
        Value = string.Empty;
    }

    public FilePathSetting(
        string key,
        string defaultValue,
        FilePathSelectionMode selectionMode = FilePathSelectionMode.File)
        : base(key, defaultValue)
    {
        SelectionMode = selectionMode;
    }

    public FilePathSetting(
        string key,
        string label,
        string description,
        string defaultValue,
        FilePathSelectionMode selectionMode = FilePathSelectionMode.File)
        : base(key, label, description, defaultValue)
    {
        SelectionMode = selectionMode;
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.CommandPalette.FilePath" },
            { "id", Key },
            { "label", string.Empty },
            { "header", Label },
            { "description", Description },
            { "value", Value ?? string.Empty },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "selectionMode", SelectionMode == FilePathSelectionMode.File ? "file" : "folder" },
            { "fileTypeFilter", FileTypeFilter },
            { "placeholder", Placeholder },
            { "validationPattern", ValidationPattern },
            { "validationErrorMessage", ValidationErrorMessage },

            // A path is an ordinary string, so a host that does not know this element type can
            // fall back to a plain text box under the same id and still round-trip the value.
            {
                "fallback", new Dictionary<string, object>
                {
                    { "type", "Input.Text" },
                    { "id", Key },
                    { "title", Label },
                    { "label", Description },
                    { "value", Value ?? string.Empty },
                    { "isRequired", IsRequired },
                    { "errorMessage", ErrorMessage },
                    { "placeholder", Placeholder },
                }
            },
        };
    }

    public override void Update(JsonObject payload)
    {
        if (payload[Key] is JsonValue value && value.TryGetValue<string>(out var submittedValue))
        {
            Value = submittedValue;
        }
    }

    public override string ToState() =>
        $"\"{Key}\": {JsonSerializer.Serialize(Value, JsonSerializationContext.Default.String)}";

    private static FilePathSelectionMode ValidateSelectionMode(FilePathSelectionMode selectionMode)
    {
        if (selectionMode is not FilePathSelectionMode.File and not FilePathSelectionMode.Folder)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionMode),
                selectionMode,
                "The selection mode must be File or Folder.");
        }

        return selectionMode;
    }
}
