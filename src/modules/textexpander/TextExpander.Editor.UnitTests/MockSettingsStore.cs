// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

/// <summary>
/// Text Expander's settings storage on a mock file system. Cleanup of owned save artifacts
/// compares bytes instead of using file handles, and the settable properties inject faults
/// into atomic saves.
/// </summary>
internal sealed class MockSettingsStore : TextExpanderSettingsUtils
{
    public MockSettingsStore(MockFileSystem fileSystem = null)
        : base(fileSystem ??= new MockFileSystem())
    {
        FileSystem = fileSystem;
    }

    public MockFileSystem FileSystem { get; }

    /// <summary>Gets or sets the settings that GetSettingsOrDefault returns a copy of instead of reading the file.</summary>
    public TextExpanderSettings InitialSettings { get; set; }

    /// <summary>Gets or sets a value indicating whether read-only reads return a copy of <see cref="InitialSettings"/> too.</summary>
    public bool ReadOnlyReturnsInitialSettings { get; set; }

    /// <summary>Gets or sets a value indicating whether publishing a staged save throws once the file is in place.</summary>
    public bool ThrowAfterPublication { get; set; }

    /// <summary>Gets or sets what runs after publication, before that throw.</summary>
    public Action AfterPublication { get; set; }

    /// <summary>Gets or sets what another process writes to the settings file before compensation starts.</summary>
    public string WrittenBeforeCompensation { get; set; }

    /// <summary>Gets or sets what another process writes to the settings file before compensation quarantines it.</summary>
    public string WrittenBeforeQuarantine { get; set; }

    /// <summary>Gets or sets what another process writes to the settings file before an unexpected file is restored.</summary>
    public string WrittenBeforeRestore { get; set; }

    public bool FailCompensation { get; set; }

    public bool FailAfterCompensation { get; set; }

    public bool FailBackupCleanup { get; set; }

    public string StagedContents { get; private set; }

    public AtomicSettingsPublicationException LastError { get; private set; }

    public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = DefaultFileName)
        => InitialSettings is not null && typeof(T) == typeof(TextExpanderSettings)
            ? (T)(object)CopyOfInitialSettings()
            : base.GetSettingsOrDefault<T>(powertoy, fileName);

    public override T GetSettingsOrDefaultReadOnly<T>(string powertoy = "", string fileName = DefaultFileName)
        => ReadOnlyReturnsInitialSettings && typeof(T) == typeof(TextExpanderSettings)
            ? (T)(object)CopyOfInitialSettings()
            : base.GetSettingsOrDefaultReadOnly<T>(powertoy, fileName);

    public override TextExpanderSettings UpdateSnippetsPath(string snippetsPath)
    {
        try
        {
            return base.UpdateSnippetsPath(snippetsPath);
        }
        catch (AtomicSettingsPublicationException ex)
        {
            LastError = ex;
            throw;
        }
    }

    /// <summary>Writes the settings file as it was before the test, without the injected save faults.</summary>
    public void WriteExistingSettings(string json, string powertoy = TextExpanderSettings.ModuleName)
    {
        string path = GetSettingsFilePath(powertoy);
        FileSystem.Directory.CreateDirectory(Path.GetDirectoryName(path));
        FileSystem.File.WriteAllText(path, json);
    }

    protected override void PublishStagedSettingsFile(string stagingPath, string settingsPath, string backupPath, bool destinationExisted)
    {
        StagedContents = FileSystem.File.ReadAllText(stagingPath);
        base.PublishStagedSettingsFile(stagingPath, settingsPath, backupPath, destinationExisted);
        if (ThrowAfterPublication)
        {
            AfterPublication?.Invoke();
            throw new IOException("Injected failure after atomic settings publication.");
        }
    }

    protected override void BeforeSettingsPublicationCompensation(string stagingPath, string settingsPath, string backupPath, bool destinationExisted)
        => WriteIfSet(settingsPath, WrittenBeforeCompensation);

    protected override void BeforeSettingsCompensationQuarantine(string settingsPath, string quarantinePath, bool destinationExisted)
        => WriteIfSet(settingsPath, WrittenBeforeQuarantine);

    protected override void BeforeUnexpectedSettingsRestore(string quarantinePath, string settingsPath, bool destinationExisted)
        => WriteIfSet(settingsPath, WrittenBeforeRestore);

    protected override void CompensateFailedSettingsPublication(
        string stagingPath,
        string settingsPath,
        string backupPath,
        string displacedPath,
        bool destinationExisted,
        byte[] originalBytes,
        byte[] publishedBytes)
    {
        if (FailCompensation)
        {
            throw new IOException("Injected settings compensation failure.");
        }

        base.CompensateFailedSettingsPublication(stagingPath, settingsPath, backupPath, displacedPath, destinationExisted, originalBytes, publishedBytes);
        if (FailAfterCompensation)
        {
            throw new IOException("Injected failure after settings restoration.");
        }
    }

    protected override HandleBoundFileDeletionResult TryDeleteOwnedAtomicSaveArtifact(string path, byte[] ownedBytes)
    {
        if (FailBackupCleanup && path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            return new(HandleBoundFileDeletionStatus.PathStillPresent, path, new IOException($"Injected backup cleanup failure at '{path}'."));
        }

        if (!FileSystem.File.Exists(path))
        {
            return new(HandleBoundFileDeletionStatus.NotFound, path, null);
        }

        if (!FileSystem.File.ReadAllBytes(path).AsSpan().SequenceEqual(ownedBytes))
        {
            return new(HandleBoundFileDeletionStatus.IdentityMismatch, path, new IOException($"The mock file at '{path}' is not the owned cleanup artifact."));
        }

        FileSystem.File.Delete(path);
        return new(HandleBoundFileDeletionStatus.Deleted, path, null);
    }

    private TextExpanderSettings CopyOfInitialSettings()
        => JsonSerializer.Deserialize(InitialSettings.ToJsonString(), SettingsSerializationContext.Default.TextExpanderSettings);

    private void WriteIfSet(string settingsPath, string contents)
    {
        if (contents is not null)
        {
            FileSystem.File.WriteAllText(settingsPath, contents);
        }
    }
}
