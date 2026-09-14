// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

public sealed class FileChangeRow(FileChange change)
{
    public string RelativePath => change.RelativePath;

    public FileChangeKind Kind => change.Kind;

    public long? BeforeSize => change.Before?.Size;

    public long? AfterSize => change.After?.Size;

    public string BeforePreview => change.Before?.Preview ?? "File did not exist before this run.";

    public string AfterPreview => change.After?.Preview ?? "File was deleted in this run.";

    public bool CanExport => change.After is not null;

    public bool IsSelected { get; set; } = change.Kind is FileChangeKind.Added or FileChangeKind.Modified;
}
