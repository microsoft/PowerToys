// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    private bool _consumerSettingsLoaded;
    private bool _consumerSettingsBusy;
    private bool _isPowerScriptsPolicyControlled;
    private bool _moduleCatalogueBusy;
    private int _moduleCatalogueGeneration;

    public PowerScriptConsumerItem AdvancedPasteConsumer { get; } = new("advanced-paste");

    public PowerScriptConsumerItem KeyboardManagerConsumer { get; } = new("keyboard-manager");

    public PowerScriptConsumerItem FileExplorerConsumer { get; } = new("file-explorer");

    public PowerScriptConsumerItem CommandPaletteConsumer { get; } = new("command-palette");

    public bool CanChangePowerScriptsEnabled => AreConsumerSettingsReady && !_isPowerScriptsPolicyControlled;

    public bool AreConsumerSettingsReady => _consumerSettingsLoaded && !_consumerSettingsBusy;

    public bool IsPowerScriptsPolicyControlled => _isPowerScriptsPolicyControlled;

    public bool IsModuleCatalogueLoading => _moduleCatalogueBusy;

    public bool AreModuleCataloguesReady => AreConsumerSettingsReady && !_moduleCatalogueBusy;

    public Task RefreshConsumersAsync() => UpdateConsumersAsync(["consumers", "list"]);

    public Task SetConsumerEnabledAsync(PowerScriptConsumerItem consumer, bool enabled) =>
        UpdateConsumersAsync(["consumers", "set", consumer.Id, enabled ? "on" : "off"]);

    public async Task RefreshModuleScriptsAsync()
    {
        var generation = ++_moduleCatalogueGeneration;
        PowerScriptConsumerItem[] consumers = [AdvancedPasteConsumer, KeyboardManagerConsumer, CommandPaletteConsumer];
        foreach (var consumer in consumers)
        {
            consumer.ClearApplicableScripts("PowerScripts_ModuleScriptsLoading");
        }

        if (!AreConsumerSettingsReady || !consumers.Any(consumer => consumer.IsEnabled))
        {
            _moduleCatalogueBusy = false;
            NotifyConsumerAvailability();
            return;
        }

        _moduleCatalogueBusy = true;
        NotifyConsumerAvailability();
        try
        {
            var root = ScriptsFolder;

            // Use normal discovery: the management catalogue also includes disabled runtimes and invalid packages.
            var json = await InvokeHostAsync(["list", "--json", "--root", root]);
            var scripts = JsonSerializer.Deserialize<List<PowerScriptListItem>>(json, JsonOptions)
                ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            if (generation != _moduleCatalogueGeneration || !AreConsumerSettingsReady)
            {
                return;
            }

            if (!string.Equals(root, ScriptsFolder, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(GetResourceString("PowerScripts_ModuleScriptsRootChanged"));
            }

            foreach (var script in scripts)
            {
                ApplyMxcSettingsToScript(script);
            }

            foreach (var consumer in consumers)
            {
                consumer.UpdateApplicableScripts(scripts);
            }
        }
        catch
        {
            if (generation == _moduleCatalogueGeneration)
            {
                foreach (var consumer in consumers)
                {
                    consumer.ClearApplicableScripts("PowerScripts_ModuleScriptsUnavailable");
                }
            }

            throw;
        }
        finally
        {
            if (generation == _moduleCatalogueGeneration)
            {
                _moduleCatalogueBusy = false;
                NotifyConsumerAvailability();
            }
        }
    }

    private async Task UpdateConsumersAsync(string[] arguments)
    {
        if (_consumerSettingsBusy)
        {
            return;
        }

        _consumerSettingsBusy = true;
        NotifyConsumerAvailability();
        try
        {
            var json = await InvokeHostAsync(arguments);
            var snapshot = JsonSerializer.Deserialize<ConsumerSettingsSnapshot>(json, JsonOptions)
                ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            var advancedPaste = snapshot.Consumers?.Single(item => item.Id == AdvancedPasteConsumer.Id)
                ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            var keyboardManager = snapshot.Consumers.Single(item => item.Id == KeyboardManagerConsumer.Id);
            var fileExplorer = snapshot.Consumers.Single(item => item.Id == FileExplorerConsumer.Id);
            var commandPalette = snapshot.Consumers.Single(item => item.Id == CommandPaletteConsumer.Id);
            _isEnabled = snapshot.IsPowerScriptsEnabled;
            _isPowerScriptsPolicyControlled = snapshot.IsPowerScriptsPolicyControlled;
            AdvancedPasteConsumer.Update(advancedPaste);
            KeyboardManagerConsumer.Update(keyboardManager);
            FileExplorerConsumer.Update(fileExplorer);
            CommandPaletteConsumer.Update(commandPalette);
            _consumerSettingsLoaded = true;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ExecutionSettingsSummary));
            OnPropertyChanged(nameof(IsPowerScriptsPolicyControlled));
        }
        catch
        {
            _consumerSettingsLoaded = false;
            AdvancedPasteConsumer.ClearApplicableScripts("PowerScripts_ModuleScriptsUnavailable");
            KeyboardManagerConsumer.ClearApplicableScripts("PowerScripts_ModuleScriptsUnavailable");
            CommandPaletteConsumer.ClearApplicableScripts("PowerScripts_ModuleScriptsUnavailable");
            throw;
        }
        finally
        {
            _consumerSettingsBusy = false;
            NotifyConsumerAvailability();
        }
    }

    private void NotifyConsumerAvailability()
    {
        OnPropertyChanged(nameof(AreConsumerSettingsReady));
        OnPropertyChanged(nameof(CanChangePowerScriptsEnabled));
        OnPropertyChanged(nameof(AreModuleCataloguesReady));
        OnPropertyChanged(nameof(IsModuleCatalogueLoading));
    }

    private sealed record ConsumerSettingsSnapshot(
        bool IsPowerScriptsEnabled,
        bool IsPowerScriptsPolicyControlled,
        ConsumerPreferenceSnapshot[] Consumers);

    public sealed record ConsumerPreferenceSnapshot(
        string Id,
        bool UserEnabled,
        bool IsEnabled,
        bool CanChange,
        bool IsPolicyControlled,
        string DisabledReason);

    public sealed class PowerScriptConsumerItem : Observable
    {
        private ConsumerPreferenceSnapshot _state;
        private bool _isExpanded;
        private string _catalogueStatusResource = "PowerScripts_ModuleScriptsLoading";

        public PowerScriptConsumerItem(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public bool IsEnabled => _state?.IsEnabled == true;

        public bool CanChange => _state?.CanChange == true;

        public IReadOnlyList<PowerScriptListItem> ApplicableScripts { get; private set; } = Array.Empty<PowerScriptListItem>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get
            {
                var description = GetResourceString(Id switch
                {
                    "advanced-paste" => "PowerScripts_AdvancedPasteScriptsDescription",
                    "keyboard-manager" => "PowerScripts_KeyboardManagerScriptsDescription",
                    "command-palette" => "PowerScripts_CommandPaletteScriptsDescription",
                    _ => throw new InvalidOperationException("Unknown PowerScripts consumer."),
                });
                return !string.IsNullOrEmpty(_state?.DisabledReason) || _state?.IsPolicyControlled == true
                    ? description + Environment.NewLine + Status
                    : description;
            }
        }

        public bool HasCatalogueStatus => ApplicableScripts.Count == 0;

        public string CatalogueStatus => GetResourceString(IsEnabled
            ? _catalogueStatusResource
            : "PowerScripts_ModuleScriptsEnable");

        public string Status => GetResourceString(_state?.DisabledReason switch
        {
            "module-disabled" => "PowerScripts_ConsumerModuleDisabled",
            "parent-policy-disabled" => "PowerScripts_ConsumerParentPolicyDisabled",
            _ when _state?.IsPolicyControlled == true => "PowerScripts_ConsumerManaged",
            _ => "PowerScripts_ConsumerDescription",
        });

        public void Update(ConsumerPreferenceSnapshot state)
        {
            _state = state;
            if (!IsEnabled)
            {
                IsExpanded = false;
                ClearApplicableScripts("PowerScripts_ModuleScriptsLoading");
            }

            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(CanChange));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(CatalogueStatus));
        }

        public void UpdateApplicableScripts(IEnumerable<PowerScriptListItem> scripts)
        {
            ApplicableScripts = IsEnabled
                ? scripts.Where(IsApplicable)
                    .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(script => script.Id, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<PowerScriptListItem>();
            _catalogueStatusResource = "PowerScripts_ModuleScriptsEmpty";
            NotifyCatalogueStatus();
        }

        internal void ClearApplicableScripts(string statusResource)
        {
            ApplicableScripts = Array.Empty<PowerScriptListItem>();
            _catalogueStatusResource = statusResource;
            NotifyCatalogueStatus();
        }

        private bool IsApplicable(PowerScriptListItem script)
        {
            var input = script.Io?.Input?.ToLowerInvariant();
            var output = script.Io?.Output?.ToLowerInvariant();
            if (input is not ("none" or "text" or "files" or "image" or "audio") ||
                output is not ("none" or "text" or "files" or "image" or "audio"))
            {
                return false;
            }

            return Id switch
            {
                "advanced-paste" => input != "none" && output != "none",
                "keyboard-manager" => input == "none",
                "command-palette" => input == "none" && output is "none" or "text",
                _ => throw new InvalidOperationException("Unknown PowerScripts consumer."),
            };
        }

        private void NotifyCatalogueStatus()
        {
            OnPropertyChanged(nameof(ApplicableScripts));
            OnPropertyChanged(nameof(HasCatalogueStatus));
            OnPropertyChanged(nameof(CatalogueStatus));
        }
    }
}
