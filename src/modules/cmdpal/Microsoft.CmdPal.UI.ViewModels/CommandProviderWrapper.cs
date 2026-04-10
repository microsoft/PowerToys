// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandProviderWrapper : ICommandProviderContext
{
    public bool IsExtension => Extension is not null;

    private readonly bool isValid;

    private readonly ExtensionObject<ICommandProvider> _commandProvider;

    private readonly TaskScheduler _taskScheduler;

    private readonly ICommandProviderCache? _commandProviderCache;

    private readonly HotkeyManager _hotkeyManager;

    private readonly AliasManager _aliasManager;

    private readonly ILogger<CommandProviderWrapper> _logger;

    public TopLevelViewModel[] TopLevelItems { get; private set; } = [];

    public TopLevelViewModel[] FallbackItems { get; private set; } = [];

    public TopLevelViewModel[] DockBandItems { get; private set; } = [];

    public string DisplayName { get; private set; } = string.Empty;

    public IExtensionWrapper? Extension { get; }

    public CommandPaletteHost ExtensionHost { get; private set; }

    public event TypedEventHandler<CommandProviderWrapper, IItemsChangedEventArgs>? CommandsChanged;

    public string Id { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public CommandSettingsViewModel? Settings { get; private set; }

    public bool IsActive { get; private set; }

    public string ProviderId => string.IsNullOrEmpty(Extension?.ExtensionUniqueId) ? Id : Extension.ExtensionUniqueId;

    public bool SupportsPinning { get; private set; }

    public TopLevelItemPageContext TopLevelPageContext { get; }

    public CommandProviderWrapper(ICommandProvider provider, TaskScheduler mainThread, HotkeyManager hotkeyManager, AliasManager aliasManager, ILogger<CommandProviderWrapper> logger)
    {
        // This ctor is only used for in-proc builtin commands. So the Unsafe!
        // calls are pretty dang safe actually.
        _commandProvider = new(provider);
        _taskScheduler = mainThread;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _logger = logger;
        TopLevelPageContext = new(this, _taskScheduler);

        // Hook the extension back into us
        ExtensionHost = new CommandPaletteHost(provider);
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

        LogInitializedBuiltInProvider(ProviderId);
    }

    public CommandProviderWrapper(IExtensionWrapper extension, TaskScheduler mainThread, HotkeyManager hotkeyManager, AliasManager aliasManager, ILogger<CommandProviderWrapper> logger, ICommandProviderCache commandProviderCache)
    {
        _taskScheduler = mainThread;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _logger = logger;
        _commandProviderCache = commandProviderCache;
        TopLevelPageContext = new(this, _taskScheduler);

        Extension = extension;
        ExtensionHost = new CommandPaletteHost(extension);
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

            LogInitializedExtensionProvider(Extension.PackageFamilyName, Extension.ExtensionUniqueId);
        }
        catch (Exception e)
        {
            LogFailedToInitializeExtensionProvider(Extension.PackageFamilyName, e);
        }

        isValid = true;
    }

    /// <summary>
    /// Constructor for JS-hosted extensions where the <see cref="ICommandProvider"/>
    /// has already been resolved by the caller.
    /// </summary>
    public CommandProviderWrapper(IExtensionWrapper extension, ICommandProvider? resolvedProvider, TaskScheduler mainThread, HotkeyManager hotkeyManager, AliasManager aliasManager, ILogger<CommandProviderWrapper> logger, ICommandProviderCache? commandProviderCache = null)
    {
        _taskScheduler = mainThread;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _logger = logger;
        _commandProviderCache = commandProviderCache;
        TopLevelPageContext = new(this, _taskScheduler);

        Extension = extension;
        ExtensionHost = new CommandPaletteHost(extension);

        if (resolvedProvider is null)
        {
            _commandProvider = new(null);
            isValid = false;
            return;
        }

        _commandProvider = new(resolvedProvider);

        try
        {
            var model = _commandProvider.Unsafe!;
            model.InitializeWithHost(ExtensionHost);
            model.ItemsChanged += CommandProvider_ItemsChanged;

            isValid = true;

            LogInitializedExtensionProvider(Extension.PackageFamilyName, Extension.ExtensionUniqueId);
        }
        catch (Exception e)
        {
            LogFailedToInitializeExtensionProvider(Extension.PackageFamilyName, e);
        }
    }

    private ProviderSettings GetProviderSettings(SettingsModel settings)
    {
        if (!settings.ProviderSettings.TryGetValue(ProviderId, out var ps))
        {
            ps = new ProviderSettings();
        }

        return ps.WithConnection(this);
    }

    public async Task LoadTopLevelCommands(ISettingsService settingsService, WeakReference<IPageContext> pageContext)
    {
        if (!isValid)
        {
            IsActive = false;
            RecallFromCache();
            return;
        }

        var providerSettings = GetProviderSettings(settingsService.Settings);

        // Persist the connected provider settings (fallback commands, etc.)
        settingsService.UpdateSettings(
            s =>
            {
                if (!s.ProviderSettings.TryGetValue(ProviderId, out var ps))
                {
                    ps = new ProviderSettings();
                }

                var newPs = ps.WithConnection(this);

                return s with
                {
                    ProviderSettings = s.ProviderSettings.SetItem(ProviderId, newPs),
                };
            },
            hotReload: false);

        IsActive = providerSettings.IsEnabled;
        if (!IsActive)
        {
            RecallFromCache();
            return;
        }

        ICommandItem[]? commands = null;
        IFallbackCommandItem[]? fallbacks = null;
        ICommandItem[] dockBands = []; // do not initialize me to null
        var displayInfoInitialized = false;

        try
        {
            var model = _commandProvider.Unsafe!;

            Task<ICommandItem[]> loadTopLevelCommandsTask = new(model.TopLevelCommands);
            loadTopLevelCommandsTask.Start();
            commands = await loadTopLevelCommandsTask.ConfigureAwait(false);

            // On a BG thread here
            fallbacks = model.FallbackCommands();

            if (model is ICommandProvider2 two)
            {
                UnsafePreCacheApiAdditions(two);
            }

            if (model is ICommandProvider3 supportsDockBands)
            {
                var bands = supportsDockBands.GetDockBands();
                if (bands is not null)
                {
                    LogFoundDockBands(bands.Length, DisplayName, ProviderId);
                    dockBands = bands;
                }
            }

            ICommandItem[] pinnedCommands = [];
            ICommandProvider4? four = null;
            if (model is ICommandProvider4 definitelyFour)
            {
                four = definitelyFour; // stash this away so we don't need to QI again
                SupportsPinning = true;

                // Load pinned commands from saved settings
                pinnedCommands = LoadPinnedCommands(four, providerSettings);
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
            var objects = new TopLevelObjects(commands, fallbacks, pinnedCommands, dockBands);
            InitializeCommands(objects, settingsService, pageContext, four);

            LogLoadedCommands(DisplayName, ProviderId);
        }
        catch (Exception e)
        {
            LogFailedToLoadCommands(Extension!.PackageFamilyName, e);

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

    private record TopLevelObjects(
        ICommandItem[]? Commands,
        IFallbackCommandItem[]? Fallbacks,
        ICommandItem[]? PinnedCommands,
        ICommandItem[]? DockBands);

    private void InitializeCommands(
        TopLevelObjects objects,
        ISettingsService settingsService,
        WeakReference<IPageContext> pageContext,
        ICommandProvider4? four)
    {
        var settings = settingsService.Settings;
        var providerSettings = GetProviderSettings(settings);
        var ourContext = GetProviderContext();

        // TODO Phase 2: TopLevelViewModel and CommandItemViewModel construction
        // will be updated once those types no longer depend on IServiceProvider.
        // Bridge: wrap the explicit dependencies into a minimal IServiceProvider
        // so the existing TopLevelViewModel constructor compiles until Phase 2
        // replaces it with direct DI parameters.
        IServiceProvider bridgeServiceProvider = new SettingsOnlyServiceProvider(settingsService);

        var make = (ICommandItem? i, TopLevelType t) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), pageContext, contextMenuFactory: null);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, t, ExtensionHost, ourContext, providerSettings, bridgeServiceProvider, i, contextMenuFactory: null);
            topLevelViewModel.InitializeProperties();

            return topLevelViewModel;
        };

        var topLevelList = new List<TopLevelViewModel>();

        if (objects.Commands is not null)
        {
            topLevelList.AddRange(objects.Commands.Select(c => make(c, TopLevelType.Normal)));
        }

        if (objects.PinnedCommands is not null)
        {
            topLevelList.AddRange(objects.PinnedCommands.Select(c => make(c, TopLevelType.Normal)));
        }

        TopLevelItems = topLevelList.ToArray();

        if (objects.Fallbacks is not null)
        {
            FallbackItems = objects.Fallbacks
                .Select(c => make(c, TopLevelType.Fallback))
                .ToArray();
        }

        List<TopLevelViewModel> bands = new();
        if (objects.DockBands is not null)
        {
            // Start by adding TopLevelViewModels for all the dock bands which
            // are explicitly provided by the provider through the GetDockBands
            // API.
            foreach (var b in objects.DockBands)
            {
                var bandVm = make(b, TopLevelType.DockBand);
                bands.Add(bandVm);
            }
        }

        var dockSettings = settings.DockSettings;
        var allPinnedCommands = dockSettings.AllPinnedCommands;
        var pinnedBandsForThisProvider = allPinnedCommands.Where(c => c.ProviderId == ProviderId);

        // Track which command IDs we've already added to avoid duplicates
        // from settings that were pinned multiple times.
        HashSet<string> seenCommandIds = new(bands.Select(b => b.Id));

        foreach (var (providerId, commandId) in pinnedBandsForThisProvider)
        {
            if (!seenCommandIds.Add(commandId))
            {
                LogSkippingDuplicatePinnedDockBand(commandId, providerId);
                continue;
            }

            LogLookingForPinnedDockBand(commandId, providerId);

            // First, try to lookup the command as one of this provider's
            // top-level commands. If it's there, then we can skip a lot of
            // work and just clone it as a band.
            if (LookupTopLevelCommand(commandId) is TopLevelViewModel topLevelCommand)
            {
                LogFoundPinnedDockBandAsTopLevel(commandId, providerId);
                var bandModel = topLevelCommand.ToPinnedDockBandItem();
                var bandVm = make(bandModel, TopLevelType.DockBand);
                bands.Add(bandVm);
                continue;
            }

            // If we didn't find it as a top-level command, then we need to
            // try to get it directly from the provider and hope it supports
            // being a dock band. This is the fallback for providers that
            // don't explicitly support dock bands through GetDockBands, but
            // do support pinning commands (ICommandProvider4)
            if (four is not null)
            {
                try
                {
                    var commandItem = four.GetCommandItem(commandId);
                    if (commandItem is not null)
                    {
                        LogFoundPinnedDockBandViaApi(commandId, providerId);
                        var bandVm = make(commandItem, TopLevelType.DockBand);
                        bands.Add(bandVm);
                    }
                    else
                    {
                        LogPinnedDockBandNotFoundViaApi(commandId, providerId);
                    }
                }
                catch (Exception e)
                {
                    LogFailedToLoadPinnedDockBand(commandId, providerId, e);
                }
            }
            else
            {
                LogPinnedDockBandProviderUnsupported(commandId, providerId);
            }
        }

        DockBandItems = bands.ToArray();
    }

    private TopLevelViewModel? LookupTopLevelCommand(string commandId)
    {
        foreach (var c in TopLevelItems)
        {
            if (c.Id == commandId)
            {
                return c;
            }
        }

        return null;
    }

    private ICommandItem[] LoadPinnedCommands(ICommandProvider4 model, ProviderSettings providerSettings)
    {
        var pinnedItems = new List<ICommandItem>();

        foreach (var pinnedId in providerSettings.PinnedCommandIds)
        {
            try
            {
                var commandItem = model.GetCommandItem(pinnedId);
                if (commandItem is not null)
                {
                    pinnedItems.Add(commandItem);
                }
            }
            catch (Exception e)
            {
                LogFailedToLoadPinnedCommand(pinnedId, e);
            }
        }

        return pinnedItems.ToArray();
    }

    private void UnsafePreCacheApiAdditions(ICommandProvider2 provider)
    {
        var apiExtensions = provider.GetApiExtensionStubs();
        LogProviderApiExtensions(apiExtensions.Length);
        foreach (var a in apiExtensions)
        {
            if (a is IExtendedAttributesProvider command2)
            {
                LogFoundExtendedAttributesProvider(ProviderId);
            }
            else if (a is ICommandItem[] commands)
            {
                LogFoundCommandItemArray(ProviderId);
            }
        }
    }

    public void PinCommand(string commandId, ISettingsService settingsService)
    {
        var providerSettings = GetProviderSettings(settingsService.Settings);

        if (!providerSettings.PinnedCommandIds.Contains(commandId))
        {
            settingsService.UpdateSettings(
                s =>
                {
                    if (!s.ProviderSettings.TryGetValue(ProviderId, out var ps))
                    {
                        ps = new ProviderSettings();
                    }

                    var providerSettings = ps.WithConnection(this);
                    var newPinned = providerSettings.PinnedCommandIds.Add(commandId);
                    var newPs = providerSettings with { PinnedCommandIds = newPinned };

                    return s with
                    {
                        ProviderSettings = s.ProviderSettings.SetItem(ProviderId, newPs),
                    };
                },
                hotReload: false);

            // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
            this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
        }
    }

    public void UnpinCommand(string commandId, ISettingsService settingsService)
    {
        settingsService.UpdateSettings(
            s =>
            {
                if (!s.ProviderSettings.TryGetValue(ProviderId, out var ps))
                {
                    ps = new ProviderSettings();
                }

                var providerSettings = ps.WithConnection(this);
                var newPinned = providerSettings.PinnedCommandIds.Remove(commandId);
                var newPs = providerSettings with { PinnedCommandIds = newPinned };

                return s with
                {
                    ProviderSettings = s.ProviderSettings.SetItem(ProviderId, newPs),
                };
            },
            hotReload: false);

        // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
    }

    public void PinDockBand(string commandId, ISettingsService settingsService, Dock.DockPinSide side = Dock.DockPinSide.Start, bool? showTitles = null, bool? showSubtitles = null)
    {
        var settings = settingsService.Settings;
        var dockSettings = settings.DockSettings;

        // Prevent duplicate pins — check all sections
        if (dockSettings.StartBands.Any(b => b.CommandId == commandId && b.ProviderId == this.ProviderId) ||
            dockSettings.CenterBands.Any(b => b.CommandId == commandId && b.ProviderId == this.ProviderId) ||
            dockSettings.EndBands.Any(b => b.CommandId == commandId && b.ProviderId == this.ProviderId))
        {
            LogDockBandAlreadyPinned(commandId, this.ProviderId);
            return;
        }

        var bandSettings = new DockBandSettings
        {
            CommandId = commandId,
            ProviderId = this.ProviderId,
            ShowTitles = showTitles,
            ShowSubtitles = showSubtitles,
        };

        settingsService.UpdateSettings(
            s =>
            {
                var dockSettings = s.DockSettings;
                return s with
                {
                    DockSettings = side switch
                    {
                        Dock.DockPinSide.Center => dockSettings with { CenterBands = dockSettings.CenterBands.Add(bandSettings) },
                        Dock.DockPinSide.End => dockSettings with { EndBands = dockSettings.EndBands.Add(bandSettings) },
                        _ => dockSettings with { StartBands = dockSettings.StartBands.Add(bandSettings) },
                    },
                };
            },
            hotReload: false);

        // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
    }

    public void UnpinDockBand(string commandId, ISettingsService settingsService)
    {
        settingsService.UpdateSettings(
            s =>
            {
                var dockSettings = s.DockSettings;
                return s with
                {
                    DockSettings = dockSettings with
                    {
                        StartBands = dockSettings.StartBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId),
                        CenterBands = dockSettings.CenterBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId),
                        EndBands = dockSettings.EndBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId),
                    },
                };
            },
            hotReload: false);

        // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
    }

    public ICommandProviderContext GetProviderContext() => this;

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

    internal void PinDockBand(TopLevelViewModel bandVm)
    {
        LogPinDockBandInternal(ProviderId, bandVm.Id);

        var bands = this.DockBandItems.ToList();
        bands.Add(bandVm);
        this.DockBandItems = bands.ToArray();
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs());
    }

    // ── LoggerMessage source-generated methods ──────────────────────────
    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized command provider {ProviderId}")]
    partial void LogInitializedBuiltInProvider(string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized extension command provider {PackageFamilyName}:{ExtensionUniqueId}")]
    partial void LogInitializedExtensionProvider(string packageFamilyName, string extensionUniqueId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize CommandProvider for extension {PackageFamilyName}")]
    partial void LogFailedToInitializeExtensionProvider(string packageFamilyName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} dock bands on {DisplayName} ({ProviderId})")]
    partial void LogFoundDockBands(int count, string displayName, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded commands from {DisplayName} ({ProviderId})")]
    partial void LogLoadedCommands(string displayName, string providerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load commands from extension {PackageFamilyName}")]
    partial void LogFailedToLoadCommands(string packageFamilyName, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping duplicate pinned dock band command {CommandId} for provider {ProviderId}")]
    partial void LogSkippingDuplicatePinnedDockBand(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Looking for pinned dock band command {CommandId} for provider {ProviderId}")]
    partial void LogLookingForPinnedDockBand(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found pinned dock band command {CommandId} for provider {ProviderId} as a top-level command")]
    partial void LogFoundPinnedDockBandAsTopLevel(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found pinned dock band command {CommandId} for provider {ProviderId} through ICommandProvider4 API")]
    partial void LogFoundPinnedDockBandViaApi(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pinned dock band command {CommandId} for provider {ProviderId} not found through ICommandProvider4 API")]
    partial void LogPinnedDockBandNotFoundViaApi(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load pinned dock band command {CommandId} for provider {ProviderId}")]
    partial void LogFailedToLoadPinnedDockBand(string commandId, string providerId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pinned dock band command {CommandId} for provider {ProviderId} not found as top-level and provider does not support ICommandProvider4")]
    partial void LogPinnedDockBandProviderUnsupported(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load pinned command {PinnedId}")]
    partial void LogFailedToLoadPinnedCommand(string pinnedId, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Provider supports {Count} API extension stubs")]
    partial void LogProviderApiExtensions(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ProviderId}: Found an IExtendedAttributesProvider")]
    partial void LogFoundExtendedAttributesProvider(string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ProviderId}: Found an ICommandItem[]")]
    partial void LogFoundCommandItemArray(string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dock band '{CommandId}' from provider '{ProviderId}' is already pinned; skipping")]
    partial void LogDockBandAlreadyPinned(string commandId, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PinDockBand: {ProviderId} - {BandId}")]
    partial void LogPinDockBandInternal(string providerId, string bandId);

    /// <summary>
    /// Temporary Phase 1 bridge: wraps <see cref="ISettingsService"/> into an
    /// <see cref="IServiceProvider"/> so that <see cref="TopLevelViewModel"/> can
    /// resolve it via <c>GetRequiredService</c>. Remove in Phase 2 when
    /// TopLevelViewModel accepts explicit DI parameters.
    /// </summary>
    private sealed class SettingsOnlyServiceProvider(Services.ISettingsService settingsService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(Services.ISettingsService))
            {
                return settingsService;
            }

            return null;
        }
    }
}
