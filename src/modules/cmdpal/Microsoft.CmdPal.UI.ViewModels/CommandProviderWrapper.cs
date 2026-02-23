// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandProviderWrapper : ICommandProviderContext
{
    public bool IsExtension => Extension is not null;

    private readonly bool isValid;

    private readonly ExtensionObject<ICommandProvider> _commandProvider;

    private readonly TaskScheduler _taskScheduler;

    private readonly ICommandProviderCache? _commandProviderCache;

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

    public CommandProviderWrapper(ICommandProvider provider, TaskScheduler mainThread)
    {
        // This ctor is only used for in-proc builtin commands. So the Unsafe!
        // calls are pretty dang safe actually.
        _commandProvider = new(provider);
        _taskScheduler = mainThread;
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

        Logger.LogDebug($"Initialized command provider {ProviderId}");
    }

    public CommandProviderWrapper(IExtensionWrapper extension, TaskScheduler mainThread, ICommandProviderCache commandProviderCache)
    {
        _taskScheduler = mainThread;
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

            Logger.LogDebug($"Initialized extension command provider {Extension.PackageFamilyName}:{Extension.ExtensionUniqueId}");
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to initialize CommandProvider for extension.");
            Logger.LogError($"Extension was {Extension!.PackageFamilyName}");
            Logger.LogError(e.ToString());
        }

        isValid = true;
    }

    private ProviderSettings GetProviderSettings(SettingsModel settings)
    {
        return settings.GetProviderSettings(this);
    }

    public async Task LoadTopLevelCommands(IServiceProvider serviceProvider)
    {
        if (!isValid)
        {
            IsActive = false;
            RecallFromCache();
            return;
        }

        var settings = serviceProvider.GetService<SettingsModel>()!;

        var providerSettings = GetProviderSettings(settings);
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
                    Logger.LogDebug($"Found {bands.Length} bands on {DisplayName} ({ProviderId}) ");
                    dockBands = bands;
                }
            }

            ICommandItem[] pinnedCommands = [];
            ICommandProvider4? four = null;
            if (model is ICommandProvider4 defintelyFour)
            {
                four = defintelyFour; // stash this away so we don't need to QI again
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
            InitializeCommands(objects, serviceProvider, four);

            Logger.LogDebug($"Loaded commands from {DisplayName} ({ProviderId})");
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load commands from extension");
            Logger.LogError($"Extension was {Extension!.PackageFamilyName}");
            Logger.LogError(e.ToString());

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
        IServiceProvider serviceProvider,
        ICommandProvider4? four)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        var contextMenuFactory = serviceProvider.GetService<IContextMenuFactory>()!;
        var state = serviceProvider.GetService<AppStateModel>()!;
        var providerSettings = GetProviderSettings(settings);
        var ourContext = GetProviderContext();
        WeakReference<IPageContext> pageContext = new(this.TopLevelPageContext);
        var make = (ICommandItem? i, TopLevelType t) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), pageContext, contextMenuFactory: contextMenuFactory);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, t, ExtensionHost, ourContext, settings, providerSettings, serviceProvider, i);
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
        foreach (var (providerId, commandId) in pinnedBandsForThisProvider)
        {
            Logger.LogDebug($"Looking for pinned dock band command {commandId} for provider {providerId}");

            // First, try to lookup the command as one of this provider's
            // top-level commands. If it's there, then we can skip a lot of
            // work and just clone it as a band.
            if (LookupTopLevelCommand(commandId) is TopLevelViewModel topLevelCommand)
            {
                Logger.LogDebug($"Found pinned dock band command {commandId} for provider {providerId} as a top-level command");
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
                        Logger.LogDebug($"Found pinned dock band command {commandId} for provider {providerId} through ICommandProvider4 API");
                        var bandVm = make(commandItem, TopLevelType.DockBand);
                        bands.Add(bandVm);
                    }
                    else
                    {
                        Logger.LogWarning($"Couldn't find pinned dock band command {commandId} for provider {providerId} through ICommandProvider4 API. This command won't be shown as a dock band.");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load pinned dock band command {commandId} for provider {providerId}: {e.Message}");
                }
            }
            else
            {
                Logger.LogWarning($"Couldn't find pinned dock band command {commandId} for provider {providerId} as a top-level command, and provider doesn't support ICommandProvider4 API to get it directly. This command won't be shown as a dock band.");
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
                Logger.LogError($"Failed to load pinned command {pinnedId}: {e.Message}");
            }
        }

        return pinnedItems.ToArray();
    }

    private void UnsafePreCacheApiAdditions(ICommandProvider2 provider)
    {
        var apiExtensions = provider.GetApiExtensionStubs();
        Logger.LogDebug($"Provider supports {apiExtensions.Length} extensions");
        foreach (var a in apiExtensions)
        {
            if (a is IExtendedAttributesProvider command2)
            {
                Logger.LogDebug($"{ProviderId}: Found an IExtendedAttributesProvider");
            }
            else if (a is ICommandItem[] commands)
            {
                Logger.LogDebug($"{ProviderId}: Found an ICommandItem[]");
            }
        }
    }

    public void PinCommand(string commandId, IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        var providerSettings = GetProviderSettings(settings);

        if (!providerSettings.PinnedCommandIds.Contains(commandId))
        {
            providerSettings.PinnedCommandIds.Add(commandId);
            SettingsModel.SaveSettings(settings);

            // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
            this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
        }
    }

    public void UnpinCommand(string commandId, IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        var providerSettings = GetProviderSettings(settings);

        if (providerSettings.PinnedCommandIds.Remove(commandId))
        {
            SettingsModel.SaveSettings(settings);

            // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
            this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
        }
    }

    public void PinDockBand(string commandId, IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        var bandSettings = new DockBandSettings
        {
            CommandId = commandId,
            ProviderId = this.ProviderId,
        };
        settings.DockSettings.StartBands.Add(bandSettings);
        SettingsModel.SaveSettings(settings);

        // Raise CommandsChanged so the TopLevelCommandManager reloads our commands
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs(-1));
    }

    public void UnpinDockBand(string commandId, IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        settings.DockSettings.StartBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId);
        settings.DockSettings.CenterBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId);
        settings.DockSettings.EndBands.RemoveAll(b => b.CommandId == commandId && b.ProviderId == ProviderId);
        SettingsModel.SaveSettings(settings);

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
        Logger.LogDebug($"CommandProviderWrapper.PinDockBand: {ProviderId} - {bandVm.Id}");

        var bands = this.DockBandItems.ToList();
        bands.Add(bandVm);
        this.DockBandItems = bands.ToArray();
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs());
    }
}
