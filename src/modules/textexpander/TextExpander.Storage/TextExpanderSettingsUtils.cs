// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.TextExpander.Storage
{
    public class TextExpanderSettingsUtils : SettingsUtils
    {
        private static readonly TimeSpan AtomicSettingsClaimTimeout = TimeSpan.FromSeconds(10);
        private readonly IFile _file;
        private readonly SettingPath _settingsPath;

        public TextExpanderSettingsUtils()
            : this(new FileSystem())
        {
        }

        public TextExpanderSettingsUtils(IFileSystem fileSystem, System.Text.Json.JsonSerializerOptions? serializerOptions = null)
            : base(fileSystem, serializerOptions)
        {
            _file = fileSystem.File;
            _settingsPath = new SettingPath(fileSystem.Directory, fileSystem.Path);
        }

        /// <summary>Saves all of <paramref name="jsonSettings"/>, atomically and under the settings claim.</summary>
        public override void SaveSettingsOrThrow(string jsonSettings, string powertoy = "", string fileName = DefaultFileName)
        {
            try
            {
                SaveSettingsAtomically(jsonSettings ?? throw new ArgumentNullException(nameof(jsonSettings)), powertoy, fileName);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while saving {powertoy} settings.", e);
                throw;
            }
        }

        /// <summary>Saves like <see cref="SaveSettingsOrThrow"/>, but logs a failure instead of throwing it, as the base class does.</summary>
        public override void SaveSettings(string jsonSettings, string powertoy = "", string fileName = DefaultFileName)
        {
            try
            {
                SaveSettingsAtomically(jsonSettings ?? throw new ArgumentNullException(nameof(jsonSettings)), powertoy, fileName);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while saving {powertoy} settings.", e);
            }
        }

        public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = DefaultFileName)
        {
            using (AcquireAtomicSettingsClaim(GetSettingsFilePath(powertoy, fileName), AtomicSettingsClaimTimeout))
            {
                return base.GetSettingsOrDefault<T>(powertoy, fileName);
            }
        }

        public override T GetSettingsOrDefaultReadOnly<T>(string powertoy = "", string fileName = DefaultFileName)
        {
            // A replace can briefly hide the destination; don't mistake an in-flight save for missing settings.
            using (AcquireAtomicSettingsClaim(GetSettingsFilePath(powertoy, fileName), AtomicSettingsClaimTimeout))
            {
                return base.GetSettingsOrDefaultReadOnly<T>(powertoy, fileName);
            }
        }

        public virtual TextExpanderSettings UpdateOptions(Action<TextExpanderProperties> update)
        {
            ArgumentNullException.ThrowIfNull(update);
            string path = GetSettingsFilePath(TextExpanderSettings.ModuleName);
            using (AcquireAtomicSettingsClaim(path, AtomicSettingsClaimTimeout))
            {
                TextExpanderSettings settings = GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
                update(settings.Properties);
                SaveSettingsAtomically(settings.ToJsonString(), TextExpanderSettings.ModuleName, DefaultFileName);
                return settings;
            }
        }

        /// <summary>
        /// Sets the snippet folder on the latest settings, keeping options another process saved meanwhile. A claim
        /// it can't get is reported, like a failed publication, as an <see cref="AtomicSettingsPublicationException"/>
        /// whose outcome is uncertain.
        /// </summary>
        public virtual TextExpanderSettings UpdateSnippetsPath(string snippetsPath)
        {
            ArgumentNullException.ThrowIfNull(snippetsPath);
            using (AcquirePublicationClaim(GetSettingsFilePath(TextExpanderSettings.ModuleName)))
            {
                TextExpanderSettings settings = GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
                settings.Properties.SnippetsPath.Value = snippetsPath;
                SaveSettingsAtomically(settings.ToJsonString(), TextExpanderSettings.ModuleName, DefaultFileName);
                return settings;
            }
        }

        private readonly record struct AtomicFileIdentity(bool Exists, byte[] Bytes)
        {
            public static AtomicFileIdentity Missing { get; } = new(false, Array.Empty<byte>());

            public bool SameAs(AtomicFileIdentity other)
                => Exists == other.Exists
                    && (!Exists || Bytes.AsSpan().SequenceEqual(other.Bytes));

            public bool SameAs(byte[] bytes)
                => Exists && Bytes.AsSpan().SequenceEqual(bytes);
        }

        private readonly record struct AtomicArtifactCleanupResult(
            IReadOnlyList<string> RecoveryPaths,
            Exception? Error)
        {
            public bool Succeeded => Error is null;
        }

        private void SaveSettingsAtomically(string jsonSettings, string powertoy, string fileName)
        {
            string settingsPath = _settingsPath.GetSettingsPath(powertoy, fileName);
            string? directory = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new IOException($"The settings folder for '{settingsPath}' could not be resolved.");
            }

            using (AcquirePublicationClaim(settingsPath))
            {
                SaveSettingsAtomicallyUnderClaim(jsonSettings, powertoy, settingsPath, directory);
            }
        }

        /// <summary>Takes the settings claim for a save. Not getting it is reported as a publication whose outcome is uncertain.</summary>
        private static IDisposable AcquirePublicationClaim(string settingsPath)
        {
            try
            {
                return AcquireAtomicSettingsClaim(settingsPath, AtomicSettingsClaimTimeout);
            }
            catch (Exception claimError)
            {
                throw new AtomicSettingsPublicationException(
                    AtomicSettingsPublicationOutcome.StateUncertain,
                    settingsPath,
                    recoveryPaths: null,
                    claimError);
            }
        }

        private void SaveSettingsAtomicallyUnderClaim(
            string jsonSettings,
            string powertoy,
            string settingsPath,
            string directory)
        {
            AtomicFileIdentity originalIdentity;
            try
            {
                if (!_settingsPath.SettingsFolderExists(powertoy))
                {
                    _settingsPath.CreateSettingsFolder(powertoy);
                }

                originalIdentity = CaptureAtomicFileIdentity(settingsPath);
            }
            catch (Exception preparationError)
            {
                throw new AtomicSettingsPublicationException(
                    AtomicSettingsPublicationOutcome.StateUncertain,
                    settingsPath,
                    recoveryPaths: null,
                    preparationError);
            }

            byte[] publishedBytes = Encoding.UTF8.GetBytes(jsonSettings);
            string suffix = Guid.NewGuid().ToString("N");
            string stagingPath = Path.Combine(directory, $".{Path.GetFileName(settingsPath)}.{suffix}.tmp");
            string backupPath = Path.Combine(directory, $".{Path.GetFileName(settingsPath)}.{suffix}.bak");
            string displacedPath = Path.Combine(directory, $".{Path.GetFileName(settingsPath)}.{suffix}.quarantine");
            bool destinationExisted = originalIdentity.Exists;

            try
            {
                using (FileSystemStream stream = _file.Open(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(publishedBytes, 0, publishedBytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                PublishStagedSettingsFile(stagingPath, settingsPath, backupPath, destinationExisted);
                AtomicFileIdentity committedIdentity = CaptureAtomicFileIdentity(settingsPath);
                if (!committedIdentity.SameAs(publishedBytes))
                {
                    throw new IOException($"The published settings file '{settingsPath}' could not be verified.");
                }
            }
            catch (Exception publicationError)
            {
                Exception? compensationError = null;
                try
                {
                    BeforeSettingsPublicationCompensation(settingsPath);
                    CompensateFailedSettingsPublication(
                        stagingPath,
                        settingsPath,
                        backupPath,
                        displacedPath,
                        destinationExisted,
                        originalIdentity.Bytes,
                        publishedBytes);
                }
                catch (Exception ex)
                {
                    compensationError = ex;
                }

                AtomicFileIdentity finalIdentity = AtomicFileIdentity.Missing;
                Exception? verificationError = null;
                try
                {
                    finalIdentity = CaptureAtomicFileIdentity(settingsPath);
                }
                catch (Exception ex)
                {
                    verificationError = ex;
                }

                compensationError = CombineExceptions(compensationError, verificationError);
                AtomicFileIdentity displacedIdentity = AtomicFileIdentity.Missing;
                Exception? displacedVerificationError = null;
                try
                {
                    displacedIdentity = CaptureAtomicFileIdentity(displacedPath);
                }
                catch (Exception ex)
                {
                    displacedVerificationError = ex;
                }

                compensationError = CombineExceptions(compensationError, displacedVerificationError);
                bool unexpectedDisplacedFile = displacedVerificationError is not null
                    || (displacedIdentity.Exists && !displacedIdentity.SameAs(publishedBytes));
                if (verificationError is null
                    && !unexpectedDisplacedFile
                    && finalIdentity.SameAs(originalIdentity))
                {
                    AtomicArtifactCleanupResult cleanup = CleanupOwnedAtomicSaveArtifacts(
                        (stagingPath, publishedBytes),
                        (backupPath, originalIdentity.Bytes),
                        (displacedPath, publishedBytes));
                    compensationError = CombineExceptions(compensationError, cleanup.Error);
                    throw new AtomicSettingsPublicationException(
                        AtomicSettingsPublicationOutcome.PreviousSettingsVerified,
                        settingsPath,
                        cleanup.RecoveryPaths,
                        publicationError,
                        compensationError);
                }

                if (verificationError is null
                    && !unexpectedDisplacedFile
                    && finalIdentity.SameAs(publishedBytes))
                {
                    AtomicArtifactCleanupResult cleanup = CleanupOwnedAtomicSaveArtifacts(
                        (stagingPath, publishedBytes),
                        (displacedPath, publishedBytes));
                    compensationError = CombineExceptions(compensationError, cleanup.Error);
                    string originalRecoveryPath = EnsureOriginalSettingsRecovery(
                        backupPath,
                        settingsPath,
                        originalIdentity,
                        out Exception? originalRecoveryError);
                    compensationError = CombineExceptions(compensationError, originalRecoveryError);
                    throw new AtomicSettingsPublicationException(
                        AtomicSettingsPublicationOutcome.NewSettingsVerified,
                        settingsPath,
                        MergeRecoveryPaths(
                            cleanup.RecoveryPaths,
                            ExistingAtomicSaveArtifacts(backupPath, originalRecoveryPath)),
                        publicationError,
                        compensationError);
                }

                string payloadRecoveryPath = EnsurePublishedPayloadRecovery(
                    stagingPath,
                    settingsPath,
                    publishedBytes,
                    out Exception? recoveryError);
                compensationError = CombineExceptions(compensationError, recoveryError);
                string previousRecoveryPath = EnsureOriginalSettingsRecovery(
                    backupPath,
                    settingsPath,
                    originalIdentity,
                    out Exception? previousRecoveryError);
                compensationError = CombineExceptions(compensationError, previousRecoveryError);
                throw new AtomicSettingsPublicationException(
                    AtomicSettingsPublicationOutcome.StateUncertain,
                    settingsPath,
                    ExistingAtomicSaveArtifacts(backupPath, displacedPath, payloadRecoveryPath, previousRecoveryPath),
                    publicationError,
                    compensationError);
            }

            AtomicArtifactCleanupResult finalCleanup = CleanupOwnedAtomicSaveArtifacts(
                (stagingPath, publishedBytes),
                (backupPath, originalIdentity.Bytes),
                (displacedPath, publishedBytes));
            if (!finalCleanup.Succeeded)
            {
                throw new AtomicSettingsPublicationException(
                    AtomicSettingsPublicationOutcome.NewSettingsVerified,
                    settingsPath,
                    finalCleanup.RecoveryPaths,
                    finalCleanup.Error!);
            }
        }

        protected virtual void PublishStagedSettingsFile(
            string stagingPath,
            string settingsPath,
            string backupPath,
            bool destinationExisted)
        {
            if (destinationExisted)
            {
                _file.Replace(stagingPath, settingsPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                _file.Move(stagingPath, settingsPath);
            }
        }

        protected virtual void BeforeSettingsPublicationCompensation(string settingsPath)
        {
        }

        protected virtual void BeforeSettingsCompensationQuarantine(string settingsPath)
        {
        }

        protected virtual void BeforeUnexpectedSettingsRestore(string settingsPath)
        {
        }

        protected virtual void CompensateFailedSettingsPublication(
            string stagingPath,
            string settingsPath,
            string backupPath,
            string displacedPath,
            bool destinationExisted,
            byte[] originalBytes,
            byte[] publishedBytes)
        {
            var originalIdentity = new AtomicFileIdentity(destinationExisted, originalBytes);
            AtomicFileIdentity destinationIdentity = CaptureAtomicFileIdentity(settingsPath);
            if (destinationIdentity.SameAs(originalIdentity))
            {
                return;
            }

            if (destinationExisted)
            {
                AtomicFileIdentity backupIdentity = CaptureAtomicFileIdentity(backupPath);
                if (!backupIdentity.SameAs(originalIdentity))
                {
                    throw new IOException($"The settings backup '{backupPath}' could not be verified before compensation.");
                }
            }

            if (!destinationIdentity.Exists)
            {
                if (!destinationExisted)
                {
                    return;
                }

                _file.Move(backupPath, settingsPath);
                if (!CaptureAtomicFileIdentity(settingsPath).SameAs(originalIdentity))
                {
                    throw new IOException($"The previous settings file '{settingsPath}' could not be verified after compensation.");
                }

                return;
            }

            BeforeSettingsCompensationQuarantine(settingsPath);
            try
            {
                _file.Move(settingsPath, displacedPath);
            }
            catch (Exception moveError) when (moveError is FileNotFoundException or DirectoryNotFoundException)
            {
                AtomicFileIdentity currentIdentity = CaptureAtomicFileIdentity(settingsPath);
                if (currentIdentity.SameAs(originalIdentity)
                    || (!destinationExisted && !currentIdentity.Exists))
                {
                    return;
                }

                if (destinationExisted && !currentIdentity.Exists)
                {
                    _file.Move(backupPath, settingsPath);
                    if (CaptureAtomicFileIdentity(settingsPath).SameAs(originalIdentity))
                    {
                        return;
                    }
                }

                throw new IOException($"The settings file '{settingsPath}' changed while it was being quarantined for compensation.", moveError);
            }

            AtomicFileIdentity displacedIdentity = CaptureAtomicFileIdentity(displacedPath);
            if (!displacedIdentity.Exists)
            {
                throw new IOException($"The quarantined settings file '{displacedPath}' could not be verified.");
            }

            if (!displacedIdentity.SameAs(publishedBytes))
            {
                RestoreUnexpectedDisplacedSettings(
                    displacedPath,
                    settingsPath,
                    displacedIdentity,
                    destinationExisted);
                throw new IOException($"The settings file '{settingsPath}' changed before compensation and was restored without overwrite.");
            }

            if (destinationExisted)
            {
                _file.Move(backupPath, settingsPath);
                if (!CaptureAtomicFileIdentity(settingsPath).SameAs(originalIdentity))
                {
                    throw new IOException($"The previous settings file '{settingsPath}' could not be verified after compensation.");
                }

                return;
            }

            if (CaptureAtomicFileIdentity(settingsPath).Exists)
            {
                throw new IOException($"A different settings file appeared at '{settingsPath}' while compensation was in progress.");
            }
        }

        private void RestoreUnexpectedDisplacedSettings(
            string displacedPath,
            string settingsPath,
            AtomicFileIdentity displacedIdentity,
            bool destinationExisted)
        {
            try
            {
                BeforeUnexpectedSettingsRestore(settingsPath);
                _file.Move(displacedPath, settingsPath);
            }
            catch (Exception restoreError)
            {
                throw new IOException(
                    $"The unexpected settings file was preserved at '{displacedPath}' because it could not be restored safely.",
                    restoreError);
            }

            if (!CaptureAtomicFileIdentity(settingsPath).SameAs(displacedIdentity))
            {
                throw new IOException($"The unexpected settings file at '{settingsPath}' could not be verified after restoration.");
            }
        }

        private AtomicFileIdentity CaptureAtomicFileIdentity(string path)
        {
            if (!_file.Exists(path))
            {
                return AtomicFileIdentity.Missing;
            }

            return new AtomicFileIdentity(Exists: true, _file.ReadAllBytes(path));
        }

        private string EnsurePublishedPayloadRecovery(
            string stagingPath,
            string settingsPath,
            byte[] publishedBytes,
            out Exception? recoveryError)
        {
            recoveryError = null;
            try
            {
                AtomicFileIdentity stagingIdentity = CaptureAtomicFileIdentity(stagingPath);
                if (stagingIdentity.SameAs(publishedBytes))
                {
                    return stagingPath;
                }

                string? directory = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrEmpty(directory))
                {
                    throw new IOException($"The settings recovery folder for '{settingsPath}' could not be resolved.");
                }

                string recoveryPath = Path.Combine(
                    directory,
                    $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.recovery");
                using FileSystemStream stream = _file.Open(recoveryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(publishedBytes, 0, publishedBytes.Length);
                stream.Flush(flushToDisk: true);
                return recoveryPath;
            }
            catch (Exception ex)
            {
                recoveryError = ex;
                return string.Empty;
            }
        }

        private string EnsureOriginalSettingsRecovery(
            string backupPath,
            string settingsPath,
            AtomicFileIdentity originalIdentity,
            out Exception? recoveryError)
        {
            recoveryError = null;
            if (!originalIdentity.Exists)
            {
                return string.Empty;
            }

            try
            {
                if (CaptureAtomicFileIdentity(backupPath).SameAs(originalIdentity))
                {
                    return backupPath;
                }

                string? directory = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrEmpty(directory))
                {
                    throw new IOException($"The settings recovery folder for '{settingsPath}' could not be resolved.");
                }

                string recoveryPath = Path.Combine(
                    directory,
                    $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.previous");
                using FileSystemStream stream = _file.Open(recoveryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(originalIdentity.Bytes, 0, originalIdentity.Bytes.Length);
                stream.Flush(flushToDisk: true);
                return recoveryPath;
            }
            catch (Exception ex)
            {
                recoveryError = ex;
                return string.Empty;
            }
        }

        private IReadOnlyList<string> ExistingAtomicSaveArtifacts(params string[] paths)
        {
            var existing = new List<string>();
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                try
                {
                    if (_file.Exists(path))
                    {
                        existing.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not inspect settings save artifact '{path}'.", ex);
                    existing.Add(path);
                }
            }

            return existing;
        }

        private static Exception? CombineExceptions(Exception? first, Exception? second)
        {
            if (first is null)
            {
                return second;
            }

            if (second is null)
            {
                return first;
            }

            return new AggregateException(first, second);
        }

        private static IReadOnlyList<string> MergeRecoveryPaths(
            IReadOnlyList<string> first,
            IReadOnlyList<string> second)
        {
            var merged = new List<string>();
            foreach (IReadOnlyList<string> paths in new[] { first, second })
            {
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path)
                        && !merged.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        merged.Add(path);
                    }
                }
            }

            return merged;
        }

        private static IDisposable AcquireAtomicSettingsClaim(string settingsPath, TimeSpan timeout)
        {
            var mutex = new Mutex(initiallyOwned: false, AtomicSettingsClaimName(settingsPath));
            bool ownsClaim;
            try
            {
                try
                {
                    ownsClaim = mutex.WaitOne(timeout);
                }
                catch (AbandonedMutexException)
                {
                    ownsClaim = true;
                }

                if (!ownsClaim)
                {
                    throw new IOException($"Timed out waiting to save settings at '{settingsPath}'.");
                }

                return new AtomicSettingsClaim(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        private static string AtomicSettingsClaimName(string settingsPath)
        {
            string canonical = Path.GetFullPath(settingsPath).ToUpperInvariant();
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return @"Local\PowerToys.Settings.AtomicFile." + Convert.ToHexString(hash);
        }

        private AtomicArtifactCleanupResult CleanupOwnedAtomicSaveArtifacts(
            params (string Path, byte[] OwnedBytes)[] artifacts)
        {
            var recoveryPaths = new List<string>();
            var errors = new List<Exception>();
            foreach ((string path, byte[] ownedBytes) in artifacts)
            {
                HandleBoundFileDeletionResult result = TryDeleteOwnedAtomicSaveArtifact(path, ownedBytes);
                if (result.Succeeded)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(result.Path)
                    && !recoveryPaths.Contains(result.Path, StringComparer.OrdinalIgnoreCase))
                {
                    recoveryPaths.Add(result.Path);
                }

                Exception artifactError = result.Error ?? new IOException($"Could not remove settings save artifact '{path}'.");
                errors.Add(artifactError);
                Logger.LogError($"Could not remove settings save artifact '{path}'.", artifactError);
            }

            Exception? error = errors.Count switch
            {
                0 => null,
                1 => errors[0],
                _ => new AggregateException(errors),
            };
            return new AtomicArtifactCleanupResult(recoveryPaths, error);
        }

        protected virtual void BeforeOwnedAtomicSaveArtifactDisposition(string path)
        {
        }

        protected virtual HandleBoundFileDeletionResult TryDeleteOwnedAtomicSaveArtifact(string path, byte[] ownedBytes)
            => HandleBoundFileDeletion.TryDelete(
                path,
                ownedBytes,
                () => BeforeOwnedAtomicSaveArtifactDisposition(path));

        private sealed class AtomicSettingsClaim : IDisposable
        {
            private Mutex? _mutex;

            public AtomicSettingsClaim(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                Mutex? mutex = Interlocked.Exchange(ref _mutex, null);
                if (mutex is null)
                {
                    return;
                }

                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }
}
