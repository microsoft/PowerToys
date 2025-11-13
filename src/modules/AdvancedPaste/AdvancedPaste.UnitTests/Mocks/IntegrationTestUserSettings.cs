// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.UnitTests.Mocks;

/// <summary>
/// Minimal <see cref="IUserSettings"/> implementation used by integration tests that
/// need to construct the runtime Advanced Paste services.
/// </summary>
internal sealed class IntegrationTestUserSettings : IUserSettings
{
    private readonly PasteAIConfiguration _configuration;
    private readonly IReadOnlyList<AdvancedPasteCustomAction> _customActions;
    private readonly IReadOnlyList<PasteFormats> _additionalActions;

    public IntegrationTestUserSettings()
    {
        var provider = new PasteAIProviderDefinition
        {
            Id = "integration-openai",
            EnableAdvancedAI = true,
            ServiceTypeKind = AIServiceType.OpenAI,
            ModelName = "gpt-4o",
            ModerationEnabled = true,
        };

        _configuration = new PasteAIConfiguration
        {
            ActiveProviderId = provider.Id,
            Providers = new ObservableCollection<PasteAIProviderDefinition> { provider },
        };

        _customActions = Array.Empty<AdvancedPasteCustomAction>();
        _additionalActions = Array.Empty<PasteFormats>();
    }

    public bool IsAIEnabled => true;

    public bool ShowCustomPreview => false;

    public bool CloseAfterLosingFocus => false;

    public bool EnableClipboardPreview => true;

    public IReadOnlyList<AdvancedPasteCustomAction> CustomActions => _customActions;

    public IReadOnlyList<PasteFormats> AdditionalActions => _additionalActions;

    public PasteAIConfiguration PasteAIConfiguration => _configuration;

    public event EventHandler Changed;

    public Task SetActiveAIProviderAsync(string providerId)
    {
        _configuration.ActiveProviderId = providerId ?? string.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
