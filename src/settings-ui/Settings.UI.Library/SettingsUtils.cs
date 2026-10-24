// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Some functions are marked as virtual to allow mocking in unit tests.
    public class SettingsUtils
    {
        public const string DefaultFileName = "settings.json";
        private const string DefaultModuleName = "";
        private static readonly TimeSpan AtomicSettingsClaimTimeout = TimeSpan.FromSeconds(10);
        private readonly IFile _file;
        private readonly SettingPath _settingsPath;
        private readonly JsonSerializerOptions _serializerOptions;

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

        /// <summary>
        /// Gets the default instance of the <see cref="SettingsUtils"/> class for general use.
        /// Same as instantiating a new instance with the <see cref="SettingsUtils(IFileSystem?, JsonSerializerOptions?)"/> constructor with a new <see cref="FileSystem"/> object as the first argument and <c>null</c> as the second argument.
        /// </summary>
        /// <remarks>For using in tests, you should use one of the public constructors.</remarks>
        public static SettingsUtils Default { get; } = new SettingsUtils();

        private SettingsUtils()
            : this(new FileSystem())
        {
        }

        public SettingsUtils(IFileSystem? fileSystem, JsonSerializerOptions? serializerOptions = null)
            : this(fileSystem?.File!, new SettingPath(fileSystem?.Directory, fileSystem?.Path), serializerOptions)
        {
        }

        public SettingsUtils(IFile file, SettingPath settingPath, JsonSerializerOptions? serializerOptions = null)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
            _settingsPath = settingPath;
            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                MaxDepth = 0,
                IncludeFields = true,
                TypeInfoResolver = SettingsSerializationContext.Default,
            };
        }

        public bool SettingsExists(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            var settingsPath = _settingsPath.GetSettingsPath(powertoy, fileName);
            return _file.Exists(settingsPath);
        }

        public void DeleteSettings(string powertoy = "")
        {
            _settingsPath.DeleteSettings(powertoy);
        }

        public void CreateSettingsFolder(string powertoy = "")
        {
            _settingsPath.CreateSettingsFolder(powertoy);
        }

        public virtual T GetSettings<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
            where T : ISettingsConfig, new()
        {
            if (!SettingsExists(powertoy, fileName))
            {
                throw new FileNotFoundException();
            }

            // Given the file already exists, to deserialize the file and read its content.
            T deserializedSettings = GetFile<T>(powertoy, fileName);

            // If the file needs to be modified, to save the new configurations accordingly.
            if (deserializedSettings.UpgradeSettingsConfiguration())
            {
                SaveSettings(deserializedSettings.ToJsonString(), powertoy, fileName);
            }

            return deserializedSettings;
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// This function creates a file in the powertoy folder if it does not exist and returns an object with default properties.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public virtual T GetSettingsOrDefault<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
            where T : ISettingsConfig, new()
        {
            try
            {
                return GetSettings<T>(powertoy, fileName);
            }

            // Catch json deserialization exceptions when the file is corrupt and has an invalid json.
            // If there are any deserialization issues like in https://github.com/microsoft/PowerToys/issues/7500, log the error and create a new settings.json file.
            // This is different from the case where we have trailing zeros following a valid json file, which we have handled by trimming the trailing zeros.
            catch (JsonException ex)
            {
                Logger.LogError($"Exception encountered while loading {powertoy} settings.", ex);
            }
            catch (FileNotFoundException)
            {
                Logger.LogInfo($"Settings file {fileName} for {powertoy} was not found.");
            }

            // If the settings file does not exist or if the file is corrupt, to create a new object with default parameters and save it to a newly created settings file.
            T newSettingsItem = new T();
            SaveSettings(newSettingsItem.ToJsonString(), powertoy, fileName);
            return newSettingsItem;
        }

        /// <summary>
        /// Reads settings without upgrading or persisting defaults, retrying transient read or parse failures.
        /// </summary>
        public virtual T GetSettingsOrDefaultReadOnly<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
            where T : ISettingsConfig, new()
        {
            const int maxAttempts = 5;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (!SettingsExists(powertoy, fileName))
                    {
                        return new T();
                    }

                    return GetFile<T>(powertoy, fileName);
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        throw new InvalidOperationException(
                            $"Settings file {fileName} for {powertoy} could not be read consistently; no changes were made.",
                            ex);
                    }

                    Thread.Sleep(100);
                }
            }

            throw new InvalidOperationException("Settings could not be read.");
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// This function creates a file in the powertoy folder if it does not exist and returns an object with default properties.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public virtual T GetSettingsOrDefault<T, T2>(string powertoy = DefaultModuleName, string fileName = DefaultFileName, Func<object, object>? settingsUpgrader = null)
            where T : ISettingsConfig, new()
            where T2 : ISettingsConfig, new()
        {
            try
            {
                return GetSettings<T>(powertoy, fileName);
            }

            // Catch json deserialization exceptions when the file is corrupt and has an invalid json.
            // If there are any deserialization issues like in https://github.com/microsoft/PowerToys/issues/7500, log the error and create a new settings.json file.
            // This is different from the case where we have trailing zeros following a valid json file, which we have handled by trimming the trailing zeros.
            catch (JsonException ex)
            {
                Logger.LogInfo($"Settings file {fileName} for {powertoy} was unrecognized. Possibly containing an older version. Trying to read again.");

                // try to deserialize to the old format, which is presented in T2
                try
                {
                    T2 oldSettings = GetSettings<T2>(powertoy, fileName);
                    T newSettings = (T)settingsUpgrader!(oldSettings);
                    Logger.LogInfo($"Settings file {fileName} for {powertoy} was read successfully in the old format.");

                    // If the file needs to be modified, to save the new configurations accordingly.
                    if (newSettings.UpgradeSettingsConfiguration())
                    {
                        SaveSettings(newSettings.ToJsonString(), powertoy, fileName);
                    }

                    return newSettings;
                }
                catch (Exception)
                {
                    // do nothing, the problem wasn't that the settings was stored in the previous format, continue with the default settings
                    Logger.LogError($"{powertoy} settings are corrupt or the format is not supported any longer. Using default settings instead.", ex);
                }
            }
            catch (FileNotFoundException)
            {
                Logger.LogInfo($"Settings file {fileName} for {powertoy} was not found.");
            }

            // If the settings file does not exist or if the file is corrupt, to create a new object with default parameters and save it to a newly created settings file.
            T newSettingsItem = new T();
            SaveSettings(newSettingsItem.ToJsonString(), powertoy, fileName);
            return newSettingsItem;
        }

        /// <summary>
        /// Deserializes settings from a JSON file.
        /// </summary>
        /// <typeparam name="T">The settings type to deserialize. Must be registered in <see cref="SettingsSerializationContext"/>.</typeparam>
        /// <param name="powertoyFolderName">The PowerToy module folder name.</param>
        /// <param name="fileName">The settings file name.</param>
        /// <returns>Deserialized settings object of type T.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when type T is not registered in <see cref="SettingsSerializationContext"/>.
        /// All settings types must be registered with <c>[JsonSerializable(typeof(T))]</c> attribute
        /// for Native AOT compatibility.
        /// </exception>
        /// <remarks>
        /// This method uses Native AOT-compatible JSON deserialization. Type T must be registered
        /// in <see cref="SettingsSerializationContext"/> before calling this method.
        /// </remarks>
        private T GetFile<T>(string powertoyFolderName = DefaultModuleName, string fileName = DefaultFileName)
        {
            // Adding Trim('\0') to overcome possible NTFS file corruption.
            // Look at issue https://github.com/microsoft/PowerToys/issues/6413 you'll see the file has a large sum of \0 to fill up a 4096 byte buffer for writing to disk
            // This, while not totally ideal, does work around the problem by trimming the end.
            // The file itself did write the content correctly but something is off with the actual end of the file, hence the 0x00 bug
            var jsonSettingsString = _file.ReadAllText(_settingsPath.GetSettingsPath(powertoyFolderName, fileName)).Trim('\0');

            // For Native AOT compatibility, get JsonTypeInfo from the TypeInfoResolver
            var typeInfo = _serializerOptions.TypeInfoResolver?.GetTypeInfo(typeof(T), _serializerOptions);

            if (typeInfo == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).FullName} is not registered in SettingsSerializationContext. Please add it to the [JsonSerializable] attributes.");
            }

            // Use AOT-friendly deserialization
            return (T)JsonSerializer.Deserialize(jsonSettingsString, typeInfo)!;
        }

        // Save settings to a json file.
        public virtual void SaveSettings(string jsonSettings, string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            try
            {
                SaveSettingsCore(jsonSettings, powertoy, fileName);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while saving {powertoy} settings.", e);
#if DEBUG
                if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                {
                    throw;
                }
#endif
            }
        }

        public virtual void SaveSettingsOrThrow(string jsonSettings, string powertoy = DefaultModuleName, string fileName = DefaultFileName)
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

        private void SaveSettingsCore(string jsonSettings, string powertoy, string fileName)
        {
            if (jsonSettings != null)
            {
                if (!_settingsPath.SettingsFolderExists(powertoy))
                {
                    _settingsPath.CreateSettingsFolder(powertoy);
                }

                _file.WriteAllText(_settingsPath.GetSettingsPath(powertoy, fileName), jsonSettings);
            }
        }

        private void SaveSettingsAtomically(string jsonSettings, string powertoy, string fileName)
        {
            string settingsPath = _settingsPath.GetSettingsPath(powertoy, fileName);
            string? directory = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new IOException($"The settings folder for '{settingsPath}' could not be resolved.");
            }

            IDisposable claim;
            try
            {
                claim = AcquireAtomicSettingsClaim(settingsPath, AtomicSettingsClaimTimeout);
            }
            catch (Exception claimError)
            {
                throw new AtomicSettingsPublicationException(
                    AtomicSettingsPublicationOutcome.StateUncertain,
                    settingsPath,
                    recoveryPaths: null,
                    claimError);
            }

            using (claim)
            {
                SaveSettingsAtomicallyUnderClaim(jsonSettings, powertoy, settingsPath, directory);
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
                    BeforeSettingsPublicationCompensation(
                        stagingPath,
                        settingsPath,
                        backupPath,
                        destinationExisted);
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

        protected virtual void BeforeSettingsPublicationCompensation(
            string stagingPath,
            string settingsPath,
            string backupPath,
            bool destinationExisted)
        {
        }

        protected virtual void BeforeSettingsCompensationQuarantine(
            string settingsPath,
            string quarantinePath,
            bool destinationExisted)
        {
        }

        protected virtual void BeforeUnexpectedSettingsRestore(
            string quarantinePath,
            string settingsPath,
            bool destinationExisted)
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

            BeforeSettingsCompensationQuarantine(settingsPath, displacedPath, destinationExisted);
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
                BeforeUnexpectedSettingsRestore(displacedPath, settingsPath, destinationExisted);
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

        // Returns the file path to the settings file, that is exposed from the local ISettingsPath instance.
        public string GetSettingsFilePath(string powertoy = "", string fileName = "settings.json")
        {
            return _settingsPath.GetSettingsPath(powertoy, fileName);
        }

        /// <summary>
        /// Method <c>BackupSettings</c> Mostly a wrapper for SettingsBackupAndRestoreUtils.BackupSettings
        /// </summary>
        public static (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) BackupSettings()
        {
            var settingsBackupAndRestoreUtilsX = SettingsBackupAndRestoreUtils.Instance;
            var settingsUtils = Default;
            var appBasePath = Path.GetDirectoryName(settingsUtils._settingsPath.GetSettingsPath(string.Empty, string.Empty));
            string settingsBackupAndRestoreDir = settingsBackupAndRestoreUtilsX.GetSettingsBackupAndRestoreDir();

            return settingsBackupAndRestoreUtilsX.BackupSettings(appBasePath, settingsBackupAndRestoreDir, false);
        }

        /// <summary>
        /// Method <c>RestoreSettings</c> Mostly a wrapper for SettingsBackupAndRestoreUtils.RestoreSettings
        /// </summary>
        public static (bool Success, string Message, string Severity) RestoreSettings()
        {
            var settingsBackupAndRestoreUtilsX = SettingsBackupAndRestoreUtils.Instance;
            var settingsUtils = Default;
            var appBasePath = Path.GetDirectoryName(settingsUtils._settingsPath.GetSettingsPath(string.Empty, string.Empty));
            string settingsBackupAndRestoreDir = settingsBackupAndRestoreUtilsX.GetSettingsBackupAndRestoreDir();
            return settingsBackupAndRestoreUtilsX.RestoreSettings(appBasePath, settingsBackupAndRestoreDir);
        }
    }
}
