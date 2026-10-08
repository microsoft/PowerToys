// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleListSettingsPage : ContentPage
{
    private readonly Settings _settings = new();

    public SampleListSettingsPage()
    {
        Name = "Path and list settings controls";
        Icon = new IconInfo("\uE8FD");

        _settings.Add(new FilePathSetting(
            "singleExecutable",
            "Single file",
            "Enter an executable path or choose one with the file picker.",
            @"C:\Windows\System32\cmd.exe",
            FilePathSelectionMode.File)
        {
            FileTypeFilter = [".exe", ".cmd", ".bat"],
            Placeholder = "Executable path",
            ValidationPattern = @"(?i)^.+\.(exe|cmd|bat)$",
            ValidationErrorMessage = "Choose an .exe, .cmd, or .bat file.",
        });

        _settings.Add(new FilePathSetting(
            "singleFolder",
            "Single folder",
            "Enter a folder path or choose one with the folder picker.",
            @"C:\Windows\System32",
            FilePathSelectionMode.Folder)
        {
            Placeholder = "Folder path",
        });

        _settings.Add(new StringListSetting(
            "identifiers",
            "Plain strings",
            "Add identifiers containing letters, numbers, underscores, or hyphens.",
            ["alpha", "beta-item"])
        {
            Placeholder = "New identifier",
            ItemValidationPattern = "^[A-Za-z][A-Za-z0-9_-]*$",
            ItemValidationErrorMessage = "Identifiers must start with a letter and contain only letters, numbers, underscores, or hyphens.",
            PreventDuplicates = true,
            DuplicateItemErrorMessage = "Identifiers must be unique.",
        });

        _settings.Add(new FilePathListSetting(
            "searchPaths",
            "Custom search paths",
            "Add files or folders to search. The file picker is limited to executables and command files.",
            [@"C:\Windows\System32"],
            FilePathListItemType.FilesAndFolders)
        {
            FileTypeFilter = [".exe", ".cmd", ".bat"],
        });

        _settings.Add(new KeyValueListSetting(
            "variables",
            "Key/value pairs",
            "Pairs are stored as structured entries, so keys and values may contain equals signs.",
            [
                new("environment", "development"),
                new("connectionString", "Server=localhost;Database=sample"),
            ])
        {
            IsRequired = true,
            KeyPlaceholder = "Variable name",
            ValuePlaceholder = "Variable value",
            MissingKeyErrorMessage = "Every value must have a variable name.",
            KeyValidationPattern = "^[A-Za-z_][A-Za-z0-9_.-]*$",
            KeyValidationErrorMessage = "Keys must start with a letter or underscore and use only letters, numbers, dots, underscores, or hyphens.",
            ValueValidationPattern = "^.+$",
            ValueValidationErrorMessage = "Values cannot be empty.",
            PreventDuplicateKeys = true,
            DuplicateKeyErrorMessage = "Variable names must be unique.",
        });
    }

    public override IContent[] GetContent() => _settings.ToContent();
}
