// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Windows.Security.Credentials;

namespace AdvancedPaste.Settings
{
    internal sealed partial class UserSettings : IUserSettings, IDisposable
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly TaskScheduler _taskScheduler;
        private readonly IFileSystemWatcher _watcher;
        private readonly Lock _loadingSettingsLock = new();
        private readonly List<PasteFormats> _additionalActions;
        private readonly List<AdvancedPasteCustomAction> _customActions;
        private readonly List<AdvancedPastePythonScriptAction> _pythonScriptActions;
        private FileSystemWatcher _scriptFolderWatcher;
        private CancellationTokenSource _scriptFolderDebounce;
        private string _watchedScriptsFolder = string.Empty;

        private const string AdvancedPasteModuleName = "AdvancedPaste";
        private const int MaxNumberOfRetry = 5;

        private bool _disposedValue;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler Changed;

        public bool IsAIEnabled { get; private set; }

        public bool ShowCustomPreview { get; private set; }

        public bool CloseAfterLosingFocus { get; private set; }

        public bool EnableClipboardPreview { get; private set; }

        public IReadOnlyList<PasteFormats> AdditionalActions => _additionalActions;

        public IReadOnlyList<AdvancedPasteCustomAction> CustomActions => _customActions;

        public PasteAIConfiguration PasteAIConfiguration { get; private set; }

        public IReadOnlyList<AdvancedPastePythonScriptAction> PythonScriptActions => _pythonScriptActions;

        public string PythonScriptsFolder { get; private set; }

        public string PythonExecutablePath { get; private set; }

        public int PythonScriptTimeoutSeconds { get; private set; } = 30;

        public IReadOnlyDictionary<string, string> TrustedScriptHashes { get; private set; } = new Dictionary<string, string>();

        public UserSettings(IFileSystem fileSystem)
        {
            _settingsUtils = new SettingsUtils(fileSystem);

            IsAIEnabled = false;
            ShowCustomPreview = true;
            CloseAfterLosingFocus = false;
            EnableClipboardPreview = true;
            PasteAIConfiguration = new PasteAIConfiguration();
            PythonScriptsFolder = GetDefaultScriptsFolder();
            PythonExecutablePath = string.Empty;
            PythonScriptTimeoutSeconds = 30;
            _additionalActions = [];
            _customActions = [];
            _pythonScriptActions = [];
            _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(AdvancedPasteModuleName, "settings.json", OnSettingsFileChanged, fileSystem);
        }

        private static string GetDefaultScriptsFolder() =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "AdvancedPaste",
                "Scripts");

        private void OnSettingsFileChanged()
        {
            lock (_loadingSettingsLock)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                Task.Delay(TimeSpan.FromMilliseconds(500))
                    .ContinueWith(_ => LoadSettingsFromJson(), _cancellationTokenSource.Token, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);
            }
        }

        private void LoadSettingsFromJson()
        {
            lock (_loadingSettingsLock)
            {
                var retry = true;
                var retryCount = 0;

                while (retry)
                {
                    try
                    {
                        retryCount++;

                        if (!_settingsUtils.SettingsExists(AdvancedPasteModuleName))
                        {
                            Logger.LogInfo("AdvancedPaste settings.json was missing, creating a new one");
                            var defaultSettings = new AdvancedPasteSettings();
                            defaultSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName);
                        if (settings != null)
                        {
                            bool migratedLegacyEnablement = TryMigrateLegacyAIEnablement(settings);

                            void UpdateSettings()
                            {
                                var properties = settings.Properties;

                                IsAIEnabled = properties.IsAIEnabled;
                                ShowCustomPreview = properties.ShowCustomPreview;
                                CloseAfterLosingFocus = properties.CloseAfterLosingFocus;
                                EnableClipboardPreview = properties.EnableClipboardPreview;
                                PasteAIConfiguration = properties.PasteAIConfiguration ?? new PasteAIConfiguration();

                                var sourceAdditionalActions = properties.AdditionalActions;
                                (PasteFormats Format, IAdvancedPasteAction[] Actions)[] additionalActionFormats =
                                [
                                    (PasteFormats.ImageToText, [sourceAdditionalActions.ImageToText]),
                                    (PasteFormats.PasteAsTxtFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsTxtFile]),
                                    (PasteFormats.PasteAsPngFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsPngFile]),
                                    (PasteFormats.PasteAsHtmlFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsHtmlFile]),
                                    (PasteFormats.TranscodeToMp3, [sourceAdditionalActions.Transcode, sourceAdditionalActions.Transcode.TranscodeToMp3]),
                                    (PasteFormats.TranscodeToMp4, [sourceAdditionalActions.Transcode, sourceAdditionalActions.Transcode.TranscodeToMp4]),
                                ];

                                _additionalActions.Clear();
                                _additionalActions.AddRange(additionalActionFormats.Where(tuple => tuple.Actions.All(action => action.IsShown))
                                                                                   .Select(tuple => tuple.Format));

                                _customActions.Clear();
                                _customActions.AddRange(properties.CustomActions.Value.Where(customAction => customAction.IsShown && customAction.IsValid));

                                var pythonScripts = properties.PythonScripts ?? new AdvancedPastePythonScriptSettings();
                                PythonScriptsFolder = string.IsNullOrWhiteSpace(pythonScripts.ScriptsFolder)
                                    ? GetDefaultScriptsFolder()
                                    : pythonScripts.ScriptsFolder;
                                PythonExecutablePath = pythonScripts.PythonExecutablePath ?? string.Empty;
                                PythonScriptTimeoutSeconds = pythonScripts.TimeoutSeconds > 0 ? pythonScripts.TimeoutSeconds : 30;
                                TrustedScriptHashes = new Dictionary<string, string>(
                                    pythonScripts.TrustedScriptHashes ?? new Dictionary<string, string>(),
                                    StringComparer.OrdinalIgnoreCase);

                                _pythonScriptActions.Clear();
                                _pythonScriptActions.AddRange(pythonScripts.Value.Where(a => a.IsShown));

                                UpdateScriptFolderWatcher(PythonScriptsFolder);

                                Changed?.Invoke(this, EventArgs.Empty);
                            }

                            Task.Factory
                                .StartNew(UpdateSettings, CancellationToken.None, TaskCreationOptions.None, _taskScheduler)
                                .Wait();

                            if (migratedLegacyEnablement)
                            {
                                settings.Save(_settingsUtils);
                            }
                        }

                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                        }

                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(500);
                    }
                }
            }
        }

        private static bool TryMigrateLegacyAIEnablement(AdvancedPasteSettings settings)
        {
            if (settings?.Properties is null)
            {
                return false;
            }

            var properties = settings.Properties;
            bool legacyAdvancedAIConsumed = properties.TryConsumeLegacyAdvancedAIEnabled(out var advancedFlag);
            bool legacyAdvancedAIEnabled = legacyAdvancedAIConsumed && advancedFlag;
            PasswordCredential legacyCredential = TryGetLegacyOpenAICredential();

            if (legacyCredential is null)
            {
                return legacyAdvancedAIConsumed;
            }

            var configuration = properties.PasteAIConfiguration;

            if (configuration is null)
            {
                configuration = new PasteAIConfiguration();
                properties.PasteAIConfiguration = configuration;
            }

            bool configurationUpdated = false;

            var ensureResult = AdvancedPasteMigrationHelper.EnsureOpenAIProvider(configuration);
            PasteAIProviderDefinition openAIProvider = ensureResult.Provider;
            configurationUpdated |= ensureResult.Updated;

            if (legacyAdvancedAIConsumed && openAIProvider is not null && openAIProvider.EnableAdvancedAI != legacyAdvancedAIEnabled)
            {
                openAIProvider.EnableAdvancedAI = legacyAdvancedAIEnabled;
                configurationUpdated = true;
            }

            if (openAIProvider is not null)
            {
                StoreMigratedOpenAICredential(openAIProvider.Id, openAIProvider.ServiceType, legacyCredential.Password);
                RemoveLegacyOpenAICredential();
            }

            const bool shouldEnableAI = true;
            bool enabledUpdated = false;
            if (properties.IsAIEnabled != shouldEnableAI)
            {
                properties.IsAIEnabled = shouldEnableAI;
                enabledUpdated = true;
            }

            return configurationUpdated || enabledUpdated || legacyAdvancedAIConsumed;
        }

        private static PasswordCredential TryGetLegacyOpenAICredential()
        {
            try
            {
                PasswordVault vault = new();
                var credential = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                credential?.RetrievePassword();
                return credential;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void RemoveLegacyOpenAICredential()
        {
            try
            {
                PasswordVault vault = new();
                TryRemoveCredential(vault, "https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
            }
            catch (Exception)
            {
            }
        }

        private static void StoreMigratedOpenAICredential(string providerId, string serviceType, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            try
            {
                var serviceKind = serviceType.ToAIServiceType();
                if (serviceKind != AIServiceType.OpenAI)
                {
                    return;
                }

                string resource = "https://platform.openai.com/api-keys";
                string username = $"PowerToys_AdvancedPaste_PasteAI_openai_{NormalizeProviderIdentifier(providerId)}";

                PasswordVault vault = new();
                TryRemoveCredential(vault, resource, username);

                PasswordCredential credential = new(resource, username, password);
                vault.Add(credential);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to migrate legacy OpenAI credential", ex);
            }
        }

        private static void TryRemoveCredential(PasswordVault vault, string credentialResource, string credentialUserName)
        {
            try
            {
                PasswordCredential existingCred = vault.Retrieve(credentialResource, credentialUserName);
                vault.Remove(existingCred);
            }
            catch (Exception)
            {
                // Credential doesn't exist, which is fine
            }
        }

        private static string NormalizeProviderIdentifier(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return "default";
            }

            var filtered = new string(providerId.Where(char.IsLetterOrDigit).ToArray());
            return string.IsNullOrWhiteSpace(filtered) ? "default" : filtered.ToLowerInvariant();
        }

        private void UpdateScriptFolderWatcher(string folderPath)
        {
            if (string.Equals(_watchedScriptsFolder, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _scriptFolderWatcher?.Dispose();
            _scriptFolderWatcher = null;
            _watchedScriptsFolder = folderPath;

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            try
            {
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }

                _scriptFolderWatcher = new FileSystemWatcher(folderPath, "*.py")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false,
                };

                _scriptFolderWatcher.Changed += OnScriptFolderChanged;
                _scriptFolderWatcher.Created += OnScriptFolderChanged;
                _scriptFolderWatcher.Deleted += OnScriptFolderChanged;
                _scriptFolderWatcher.Renamed += OnScriptFolderChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set up script folder watcher for {folderPath}", ex);
            }
        }

        private void OnScriptFolderChanged(object sender, FileSystemEventArgs e)
        {
            lock (_loadingSettingsLock)
            {
                _scriptFolderDebounce?.Cancel();
                _scriptFolderDebounce = new CancellationTokenSource();

                Task.Delay(TimeSpan.FromMilliseconds(500))
                    .ContinueWith(
                        _ =>
                        {
                            Task.Factory
                                .StartNew(
                                    () => Changed?.Invoke(this, EventArgs.Empty),
                                    CancellationToken.None,
                                    TaskCreationOptions.None,
                                    _taskScheduler)
                                .Wait();
                        },
                        _scriptFolderDebounce.Token,
                        TaskContinuationOptions.NotOnCanceled,
                        TaskScheduler.Default);
            }
        }

        public void StoreTrustedScriptHash(string scriptPath, string hash)
        {
            lock (_loadingSettingsLock)
            {
                try
                {
                    var settings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName);
                    if (settings?.Properties?.PythonScripts is null)
                    {
                        return;
                    }

                    settings.Properties.PythonScripts.TrustedScriptHashes ??= new Dictionary<string, string>();
                    settings.Properties.PythonScripts.TrustedScriptHashes[scriptPath] = hash;
                    settings.Save(_settingsUtils);

                    // Update in-memory cache.
                    var updated = new Dictionary<string, string>(TrustedScriptHashes, StringComparer.OrdinalIgnoreCase)
                    {
                        [scriptPath] = hash,
                    };
                    TrustedScriptHashes = updated;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to store trusted script hash", ex);
                }
            }
        }

        public async Task SetActiveAIProviderAsync(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return;
            }

            await Task.Run(() =>
            {
                lock (_loadingSettingsLock)
                {
                    var settings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName);
                    var configuration = settings?.Properties?.PasteAIConfiguration;
                    var providers = configuration?.Providers;

                    if (configuration == null || providers == null || providers.Count == 0)
                    {
                        return;
                    }

                    var target = providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
                    if (target == null)
                    {
                        return;
                    }

                    if (string.Equals(configuration.ActiveProvider?.Id, providerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    configuration.ActiveProviderId = providerId;

                    foreach (var provider in providers)
                    {
                        provider.IsActive = string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase);
                    }

                    try
                    {
                        settings.Save(_settingsUtils);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to set active AI provider", ex);
                        return;
                    }

                    try
                    {
                        Task.Factory
                            .StartNew(
                                () =>
                                {
                                    PasteAIConfiguration.ActiveProviderId = providerId;

                                    if (PasteAIConfiguration.Providers is not null)
                                    {
                                        foreach (var provider in PasteAIConfiguration.Providers)
                                        {
                                            provider.IsActive = string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase);
                                        }
                                    }

                                    Changed?.Invoke(this, EventArgs.Empty);
                                },
                                CancellationToken.None,
                                TaskCreationOptions.None,
                                _taskScheduler)
                            .Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to dispatch active AI provider change", ex);
                    }
                }
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource?.Dispose();
                    _scriptFolderDebounce?.Dispose();
                    _scriptFolderWatcher?.Dispose();
                    _watcher?.Dispose();
                }

                _disposedValue = true;
            }
        }

        ~UserSettings()
        {
            Dispose(false);
        }
    }
}
