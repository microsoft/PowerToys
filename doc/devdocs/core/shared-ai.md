# PowerToys shared AI core

This document describes the shared AI stack that PowerToys modules use for providers, credentials, settings, PromptBox, and telemetry. Advanced Paste is the first in-repo consumer.

## Components

| Layer | Location | Role |
|-------|----------|------|
| Foundry Local runtime | `src/common/LanguageModelProvider/` | Foundry Local SDK 2.0 client + `ILanguageModelProvider` |
| Shared AI services | `src/common/PowerToys.AI/` | Chat providers, factory, credentials, telemetry scaffolds, config mapper |
| Settings models | `src/settings-ui/Settings.UI.Library/` | `AIConfiguration`, `AIProviderDefinition`, `AIProvidersSettings` |
| Settings UI | `AIProvidersPage` / `AIProvidersViewModel` | Global provider CRUD |
| Shared PromptBox | `src/common/Common.UI.Controls/Controls/PromptBox/` | Property/event-based prompt control |

## Global settings

Providers are stored under:

`%LocalAppData%\Microsoft\PowerToys\AIProviders\settings.json`

Module name: `AIProviders`.

On first load, `AIProvidersMigrationHelper.EnsureMigrated` copies `paste-ai-configuration` from Advanced Paste settings when the global store is empty.

API keys use Windows Credential Vault usernames:

- Preferred: `PowerToys_AI_{service}_{providerId}`
- Legacy (read + migrate): `PowerToys_AdvancedPaste_PasteAI_{service}_{providerId}`

## Consuming from a module

1. Reference `PowerToys.AI` (and optionally `Common.UI.Controls` for PromptBox).
2. Load configuration:

```csharp
AIProvidersMigrationHelper.EnsureMigrated(settingsUtils);
var settings = settingsUtils.GetSettingsOrDefault<AIProvidersSettings>(AIProvidersSettings.ModuleName);
var config = settings.Properties.AIConfiguration;
```

3. Create a provider:

```csharp
var credentials = new VaultAICredentialsProvider(() =>
{
    var active = config.ActiveProvider;
    return (active?.ServiceTypeKind ?? AIServiceType.OpenAI, active?.Id ?? string.Empty);
});

var runtime = AIProviderConfigMapper.FromConfiguration(config, credentials);
IAIChatProvider provider = new AIChatProviderFactory().CreateProvider(runtime);

var text = await provider.CompleteAsync(
    systemPrompt,
    new[] { AIChatTurn.User(userPrompt) },
    cancellationToken);
```

4. Host PromptBox with properties/events (no Advanced Paste ViewModel dependency):

```xml
<controls:PromptBox
    Text="{x:Bind ViewModel.Prompt, Mode=TwoWay}"
    IsBusy="{x:Bind ViewModel.IsBusy, Mode=OneWay}"
    PromptSubmitted="OnPromptSubmitted"
    RequestCancelled="OnRequestCancelled" />
```

5. Emit shared telemetry with your module name:

```csharp
PowerToysTelemetry.Log.WriteEvent(new AIEndpointUsageEvent(
    moduleName: "YourModule",
    providerType: runtime.ProviderType,
    modelName: runtime.Model,
    providerId: config.ActiveProviderId,
    isLocal: AIProviderConfigMapper.IsLocalProvider(runtime.ProviderType),
    durationMs: durationMs));
```

## What stays module-local

- Advanced Paste Semantic Kernel **tools/agent** path and clipboard `DataPackage` executor
- OpenAI moderation **policy** (when to call moderation, product UX)
- Domain pipelines
- Module-specific dialogs that host PromptBox