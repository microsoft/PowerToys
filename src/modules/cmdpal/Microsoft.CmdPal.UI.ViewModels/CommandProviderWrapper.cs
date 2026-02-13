// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandProviderWrapper
{
    public bool IsExtension => Extension is not null;

    private readonly bool isValid;

    private readonly ExtensionObject<ICommandProvider> _commandProvider;
    private readonly TaskScheduler _taskScheduler;
    private readonly ILogger _logger;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;

    private readonly ICommandProviderCache? _commandProviderCache;

    public TopLevelViewModel[] TopLevelItems { get; private set; } = [];

    public TopLevelViewModel[] FallbackItems { get; private set; } = [];

    public string DisplayName { get; private set; } = string.Empty;

    public IExtensionWrapper? Extension { get; }

    public CommandPaletteHost ExtensionHost { get; private set; }

    public event TypedEventHandler<CommandProviderWrapper, IItemsChangedEventArgs>? CommandsChanged;

    public string Id { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public CommandSettingsViewModel? Settings { get; private set; }

    public bool IsActive { get; private set; }

    public string ProviderId => string.IsNullOrEmpty(Extension?.ExtensionUniqueId) ? Id : Extension.ExtensionUniqueId;

    public CommandProviderWrapper(
        ICommandProvider provider,
        TaskScheduler mainThread,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        ILogger logger)
    {
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;

        // This ctor is only used for in-proc builtin commands. So the Unsafe!
        // calls are pretty dang safe actually.
        _commandProvider = new(provider);
        _taskScheduler = mainThread;
        _logger = logger;

        // Hook the extension back into us
        ExtensionHost = new CommandPaletteHost(provider, logger);
        _commandProvider.Unsafe!.InitializeWithHost(ExtensionHost);

        _commandProvider.Unsafe!.ItemsChanged += CommandProvider_ItemsChanged;

        isValid = true;
        Id = provider.Id;
        DisplayName = provider.DisplayName;
        Icon = new(provider.Icon);
        Icon.InitializeProperties();

        // Note: explicitly not InitializeProperties()ing the settings here. If
        // we do that, then we'd regress GH #38321
        Settings = new(provider.Settings, this, _taskScheduler);

        Log_InitializedCommandProvider(ProviderId);
    }

    public CommandProviderWrapper(
        IExtensionWrapper extension,
        TaskScheduler mainThread,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        ICommandProviderCache commandProviderCache,
        ILogger logger)
    {
        _taskScheduler = mainThread;
        _logger = logger;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _commandProviderCache = commandProviderCache;
        Extension = extension;
        ExtensionHost = new CommandPaletteHost(extension, logger);
        if (!Extension.IsRunning())
        {
            throw new ArgumentException("You forgot to start the extension. This is a CmdPal error - we need to make sure to call StartExtensionAsync");
        }

        var extensionImpl = extension.GetExtensionObject();
        var providerObject = extensionImpl?.GetProvider(ProviderType.Commands);
        if (providerObject is not ICommandProvider provider)
        {
            throw new ArgumentException("extension didn't actually implement ICommandProvider");
        }

        _commandProvider = new(provider);

        try
        {
            var model = _commandProvider.Unsafe!;

            // Hook the extension back into us
            model.InitializeWithHost(ExtensionHost);
            model.ItemsChanged += CommandProvider_ItemsChanged;

            isValid = true;

            Log_InitializedExtensionCommandProvider(Extension.PackageFamilyName, Extension.ExtensionUniqueId);
        }
        catch (Exception e)
        {
            Log_FailedToInitializeCommandProvider(Extension!.PackageFamilyName, e);
        }

        isValid = true;
    }

    private ProviderSettings GetProviderSettings(SettingsModel settings)
    {
        return settings.GetProviderSettings(this);
    }

    public async Task LoadTopLevelCommands(SettingsService settingsService, WeakReference<IPageContext> pageContext)
    {
        if (!isValid)
        {
            IsActive = false;
            RecallFromCache();
            return;
        }

        var settings = settingsService.CurrentSettings;

        var providerSettings = GetProviderSettings(settings);
        IsActive = providerSettings.IsEnabled;
        if (!IsActive)
        {
            RecallFromCache();
            return;
        }

        var displayInfoInitialized = false;
        try
        {
            var model = _commandProvider.Unsafe!;

            Task<ICommandItem[]> loadTopLevelCommandsTask = new(model.TopLevelCommands);
            loadTopLevelCommandsTask.Start();
            var commands = await loadTopLevelCommandsTask.ConfigureAwait(false);

            // On a BG thread here
            var fallbacks = model.FallbackCommands();

            if (model is ICommandProvider2 two)
            {
                UnsafePreCacheApiAdditions(two);
            }

            Id = model.Id;
            DisplayName = model.DisplayName;
            Icon = new(model.Icon);
            Icon.InitializeProperties();
            displayInfoInitialized = true;

            // Update cached display name
            if (_commandProviderCache is not null && Extension?.ExtensionUniqueId is not null)
            {
                _commandProviderCache.Memorize(Extension.ExtensionUniqueId, new CommandProviderCacheItem(model.DisplayName));
            }

            // Note: explicitly not InitializeProperties()ing the settings here. If
            // we do that, then we'd regress GH #38321
            Settings = new(model.Settings, this, _taskScheduler);

            // We do need to explicitly initialize commands though
            InitializeCommands(commands, fallbacks, settingsService, pageContext);

            Log_LoadedCommands(DisplayName, ProviderId);
        }
        catch (Exception e)
        {
            Log_FailedToLoadCommands(Extension!.PackageFamilyName, e);

            if (!displayInfoInitialized)
            {
                RecallFromCache();
            }
        }
    }

    private void RecallFromCache()
    {
        var cached = _commandProviderCache?.Recall(ProviderId);
        if (cached is not null)
        {
            DisplayName = cached.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = Extension?.PackageDisplayName ?? Extension?.PackageFamilyName ?? ProviderId;
        }
    }

    private void InitializeCommands(ICommandItem[] commands, IFallbackCommandItem[] fallbacks, SettingsService settingsService, WeakReference<IPageContext> pageContext)
    {
        var settings = settingsService.CurrentSettings;
        var providerSettings = GetProviderSettings(settings);

        var makeAndAdd = (ICommandItem? i, bool fallback) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), pageContext);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, fallback, ExtensionHost, ProviderId, settingsService, providerSettings, _hotkeyManager, _aliasManager, i);
            topLevelViewModel.InitializeProperties();

            return topLevelViewModel;
        };

        if (commands is not null)
        {
            TopLevelItems = commands
                .Select(c => makeAndAdd(c, false))
                .ToArray();
        }

        if (fallbacks is not null)
        {
            FallbackItems = fallbacks
                .Select(c => makeAndAdd(c, true))
                .ToArray();
        }
    }

    private void UnsafePreCacheApiAdditions(ICommandProvider2 provider)
    {
        var apiExtensions = provider.GetApiExtensionStubs();
        Log_ProviderSupportsExtensions(apiExtensions.Length);
        foreach (var a in apiExtensions)
        {
            if (a is IExtendedAttributesProvider command2)
            {
                Log_FoundExtendedAttributesProvider(ProviderId);
            }
        }
    }

    public override bool Equals(object? obj) => obj is CommandProviderWrapper wrapper && isValid == wrapper.isValid;

    public override int GetHashCode() => _commandProvider.GetHashCode();

    private void CommandProvider_ItemsChanged(object sender, IItemsChangedEventArgs args) =>

        // We don't want to handle this ourselves - we want the
        // TopLevelCommandManager to know about this, so they can remove
        // our old commands from their own list.
        //
        // In handling this, a call will be made to `LoadTopLevelCommands` to
        // retrieve the new items.
        this.CommandsChanged?.Invoke(this, args);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized command provider {providerId}")]
    private partial void Log_InitializedCommandProvider(string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized extension command provider {packageFamilyName}:{extensionUniqueId}")]
    private partial void Log_InitializedExtensionCommandProvider(string? packageFamilyName, string? extensionUniqueId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize CommandProvider for extension {packageFamilyName}")]
    private partial void Log_FailedToInitializeCommandProvider(string? packageFamilyName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded commands from {displayName} ({providerId})")]
    private partial void Log_LoadedCommands(string displayName, string providerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load commands from extension {packageFamilyName}")]
    private partial void Log_FailedToLoadCommands(string? packageFamilyName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Provider supports {extensionCount} extensions")]
    private partial void Log_ProviderSupportsExtensions(int extensionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{providerId}: Found an IExtendedAttributesProvider")]
    private partial void Log_FoundExtendedAttributesProvider(string providerId);
}
