// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Newtonsoft.Json;

namespace MouseWithoutBorders.Class
{
    internal interface IServicePairingProtector
    {
        byte[] Protect(byte[] value, byte[] entropy);

        byte[] Unprotect(byte[] value, byte[] entropy);
    }

    internal sealed class LocalSystemPairingProtector : IServicePairingProtector
    {
        public byte[] Protect(byte[] value, byte[] entropy)
        {
            return ProtectedData.Protect(value, entropy, DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] value, byte[] entropy)
        {
            return ProtectedData.Unprotect(value, entropy, DataProtectionScope.CurrentUser);
        }
    }

    internal readonly record struct ServicePairingCredential(string Key, long Revision);

    internal sealed class ServicePairingCredentialStore
    {
        internal const int CurrentFormatVersion = 1;
        internal const string ConfiguredIndicator = MouseWithoutBordersIpc.PairingConfiguredIndicator;

        private readonly string _ownerSid;
        private readonly string _basePath;
        private readonly string _directoryPath;
        private readonly string _credentialPath;
        private readonly IServicePairingProtector _protector;
        private readonly string _mutexName;
        private readonly bool _applyAccessControl;

        internal ServicePairingCredentialStore(
            string basePath,
            string ownerSid,
            IServicePairingProtector protector,
            string mutexName = null,
            bool applyAccessControl = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerSid);
            ArgumentNullException.ThrowIfNull(protector);

            _ownerSid = ownerSid;
            _basePath = basePath;
            _directoryPath = Path.Combine(basePath, ownerSid);
            _credentialPath = Path.Combine(_directoryPath, "credential.json");
            _protector = protector;
            _mutexName = mutexName ?? $@"Global\PowerToys.MWB.Pairing.Store.{CreateStableName(ownerSid)}";
            _applyAccessControl = applyAccessControl;
        }

        internal string CredentialPath => _credentialPath;

        internal void PrepareDirectory()
        {
            EnsureProtectedDirectory();
        }

        internal ServicePairingCredential Read()
        {
            using var writeLock = AcquireWriteLock();
            return ReadCore();
        }

        internal ServicePairingCredential Write(string key, long expectedRevision)
        {
            ValidateKey(key);

            using var writeLock = AcquireWriteLock();
            long currentRevision = 0;
            if (File.Exists(_credentialPath))
            {
                try
                {
                    currentRevision = ReadCore().Revision;
                }
                catch (InvalidDataException)
                {
                }
            }

            if (currentRevision != expectedRevision)
            {
                throw new InvalidOperationException("The pairing credential changed before this update completed.");
            }

            EnsureProtectedDirectory();

            var document = new CredentialDocument
            {
                Version = CurrentFormatVersion,
                OwnerSid = _ownerSid,
                Revision = checked(currentRevision + 1),
                ProtectedKey = Convert.ToBase64String(_protector.Protect(
                    Encoding.UTF8.GetBytes(key),
                    GetEntropy())),
            };

            var temporaryPath = _credentialPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var serialized = JsonConvert.SerializeObject(document);
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(serialized);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (_applyAccessControl)
                {
                    ApplyProtectedFileAcl(temporaryPath);
                }

                if (File.Exists(_credentialPath))
                {
                    File.Replace(temporaryPath, _credentialPath, null);
                }
                else
                {
                    File.Move(temporaryPath, _credentialPath);
                }

                if (_applyAccessControl)
                {
                    ApplyProtectedFileAcl(_credentialPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return new ServicePairingCredential(key, document.Revision);
        }

        internal static bool IsValidKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                !string.Equals(key, ConfiguredIndicator, StringComparison.Ordinal) &&
                !string.Equals(key, MouseWithoutBordersIpc.PairingSetupRequiredIndicator, StringComparison.Ordinal);
        }

        private ServicePairingCredential ReadCore()
        {
            EnsureNoReparsePoints();
            if (!File.Exists(_credentialPath))
            {
                throw new InvalidDataException("The protected pairing credential is not configured.");
            }

            CredentialDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CredentialDocument>(File.ReadAllText(_credentialPath));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The protected pairing credential has an invalid format.", ex);
            }

            if (document == null ||
                document.Version != CurrentFormatVersion ||
                document.Revision <= 0 ||
                !string.Equals(document.OwnerSid, _ownerSid, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(document.ProtectedKey))
            {
                throw new InvalidDataException("The protected pairing credential has an invalid scope or shape.");
            }

            try
            {
                var protectedBytes = Convert.FromBase64String(document.ProtectedKey);
                var key = Encoding.UTF8.GetString(_protector.Unprotect(protectedBytes, GetEntropy()));
                ValidateKey(key);
                return new ServicePairingCredential(key, document.Revision);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
            {
                throw new InvalidDataException("The protected pairing credential cannot be read.", ex);
            }
        }

        private void EnsureProtectedDirectory()
        {
            EnsureNoReparsePoints();
            Directory.CreateDirectory(_basePath);
            Directory.CreateDirectory(_directoryPath);
            EnsureNoReparsePoints();

            if (_applyAccessControl)
            {
                ApplyProtectedDirectoryAcl(_basePath);
                ApplyProtectedDirectoryAcl(_directoryPath);
            }
        }

        private static void ApplyProtectedDirectoryAcl(string path)
        {
            var security = CreateProtectedSecurity<DirectorySecurity>();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }

        private void EnsureNoReparsePoints()
        {
            var current = _directoryPath;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("The pairing credential path cannot contain reparse points.");
                }

                current = Path.GetDirectoryName(current);
            }

            if (File.Exists(_credentialPath) &&
                (File.GetAttributes(_credentialPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The pairing credential file cannot be a reparse point.");
            }
        }

        private void ApplyProtectedFileAcl(string path)
        {
            var security = CreateProtectedSecurity<FileSecurity>();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }

        private static T CreateProtectedSecurity<T>()
            where T : FileSystemSecurity, new()
        {
            var security = new T();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            return security;
        }

        private byte[] GetEntropy()
        {
            return Encoding.UTF8.GetBytes($"PowerToys.MouseWithoutBorders.Pairing.v{CurrentFormatVersion}:{_ownerSid}");
        }

        private IDisposable AcquireWriteLock()
        {
            var mutex = new Mutex(false, _mutexName);
            try
            {
                try
                {
                    mutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                }

                return new MutexReleaser(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        private static void ValidateKey(string key)
        {
            if (!IsValidKey(key))
            {
                throw new ArgumentException("The pairing credential must contain a non-masked value.", nameof(key));
            }
        }

        internal static string CreateStableName(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0, 24);
        }

        private sealed class CredentialDocument
        {
            public int Version { get; set; }

            public string OwnerSid { get; set; }

            public long Revision { get; set; }

            public string ProtectedKey { get; set; }
        }

        private sealed class MutexReleaser : IDisposable
        {
            private Mutex _mutex;

            internal MutexReleaser(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                var mutex = Interlocked.Exchange(ref _mutex, null);
                if (mutex != null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                }
            }
        }
    }

    internal interface IServicePairingChangeNotifier : IDisposable
    {
        void Signal();

        void Start(Action callback);
    }

    internal sealed class ServicePairingChangeNotifier : IServicePairingChangeNotifier
    {
        private readonly FileSystemWatcher _watcher;

        internal ServicePairingChangeNotifier(string credentialPath)
        {
            _watcher = new FileSystemWatcher(
                Path.GetDirectoryName(credentialPath)!,
                Path.GetFileName(credentialPath))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
        }

        public void Signal()
        {
        }

        public void Start(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            FileSystemEventHandler changed = (_, _) => callback();
            RenamedEventHandler renamed = (_, _) => callback();
            _watcher.Changed += changed;
            _watcher.Created += changed;
            _watcher.Renamed += renamed;
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }

    internal sealed class ServicePairingCredentialManager : IDisposable
    {
        [ThreadStatic]
        private static bool _updatingRuntimeCredential;

        private readonly ServicePairingCredentialStore _store;
        private readonly IServicePairingChangeNotifier _notifier;
        private readonly Action<string> _credentialChanged;
        private readonly object _stateLock = new();
        private readonly object _applyLock = new();
        private long _revision;

        internal ServicePairingCredentialManager(
            ServicePairingCredentialStore store,
            IServicePairingChangeNotifier notifier,
            Action<string> credentialChanged)
        {
            _store = store;
            _notifier = notifier;
            _credentialChanged = credentialChanged;
            _notifier.Start(ReloadIfChanged);
        }

        internal static ServicePairingCredentialManager Current { get; private set; }

        internal static bool IsProtectedMode => Current != null;

        internal static bool CanUpdateRuntimeCredential => !IsProtectedMode || _updatingRuntimeCredential;

        internal long CurrentRevision
        {
            get
            {
                lock (_stateLock)
                {
                    return _revision;
                }
            }
        }

        internal static void InitializeForLocalSystem(string ownerSid, Action<string> credentialChanged)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerSid);
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft",
                "PowerToys",
                "MouseWithoutBorders",
                "Pairing");
            var store = new ServicePairingCredentialStore(basePath, ownerSid, new LocalSystemPairingProtector());
            store.PrepareDirectory();
            Current = new ServicePairingCredentialManager(
                store,
                new ServicePairingChangeNotifier(store.CredentialPath),
                credentialChanged);
            Current.LoadAndApply();
        }

        internal bool TryLoad(out string key)
        {
            try
            {
                key = _store.Read().Key;
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException)
            {
                key = string.Empty;
                return false;
            }
        }

        internal ServicePairingCredential Save(string key, bool applyToCurrentProcess = true)
        {
            ServicePairingCredential credential;
            lock (_stateLock)
            {
                credential = _store.Write(key, _revision);
                _revision = credential.Revision;
            }

            if (applyToCurrentProcess)
            {
                ApplyCredentialIfCurrent(credential);
            }

            _notifier.Signal();
            return credential;
        }

        internal void ApplyCredentialToCurrentProcess(ServicePairingCredential credential)
        {
            ApplyCredentialIfCurrent(credential);
        }

        private void LoadAndApply()
        {
            try
            {
                var credential = _store.Read();
                lock (_stateLock)
                {
                    if (credential.Revision <= _revision)
                    {
                        return;
                    }

                    _revision = credential.Revision;
                }

                ApplyCredentialIfCurrent(credential);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException)
            {
            }
        }

        private void ReloadIfChanged()
        {
            try
            {
                ServicePairingCredential credential;
                lock (_stateLock)
                {
                    credential = _store.Read();
                    if (credential.Revision <= _revision)
                    {
                        return;
                    }

                    _revision = credential.Revision;
                }

                ApplyCredentialIfCurrent(credential);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException)
            {
            }
        }

        public void Dispose()
        {
            _notifier.Dispose();
        }

        private void ApplyCredential(string key)
        {
            _updatingRuntimeCredential = true;
            try
            {
                _credentialChanged(key);
            }
            finally
            {
                _updatingRuntimeCredential = false;
            }
        }

        private void ApplyCredentialIfCurrent(ServicePairingCredential credential)
        {
            lock (_applyLock)
            {
                lock (_stateLock)
                {
                    if (credential.Revision != _revision)
                    {
                        return;
                    }
                }

                ApplyCredential(credential.Key);
            }
        }
    }
}
