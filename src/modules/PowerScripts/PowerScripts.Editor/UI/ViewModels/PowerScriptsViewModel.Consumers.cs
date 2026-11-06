// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

    public PowerScriptConsumerItem AdvancedPasteConsumer { get; } = new("advanced-paste");

    public PowerScriptConsumerItem KeyboardManagerConsumer { get; } = new("keyboard-manager");

    public PowerScriptConsumerItem FileExplorerConsumer { get; } = new("file-explorer");

    public bool CanChangePowerScriptsEnabled => AreConsumerSettingsReady && !_isPowerScriptsPolicyControlled;

    public bool AreConsumerSettingsReady => _consumerSettingsLoaded && !_consumerSettingsBusy;

    public bool IsPowerScriptsPolicyControlled => _isPowerScriptsPolicyControlled;

    public Task RefreshConsumersAsync() => UpdateConsumersAsync(["consumers", "list"]);

    public Task SetConsumerEnabledAsync(PowerScriptConsumerItem consumer, bool enabled) =>
        UpdateConsumersAsync(["consumers", "set", consumer.Id, enabled ? "on" : "off"]);

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
            _isEnabled = snapshot.IsPowerScriptsEnabled;
            _isPowerScriptsPolicyControlled = snapshot.IsPowerScriptsPolicyControlled;
            AdvancedPasteConsumer.Update(advancedPaste);
            KeyboardManagerConsumer.Update(keyboardManager);
            FileExplorerConsumer.Update(fileExplorer);
            _consumerSettingsLoaded = true;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ExecutionSettingsSummary));
            OnPropertyChanged(nameof(IsPowerScriptsPolicyControlled));
        }
        catch
        {
            _consumerSettingsLoaded = false;
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

        public PowerScriptConsumerItem(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public bool IsEnabled => _state?.IsEnabled == true;

        public bool CanChange => _state?.CanChange == true;

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
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(CanChange));
            OnPropertyChanged(nameof(Status));
        }
    }
}
