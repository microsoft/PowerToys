// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandProviderWrapper
{
    public bool IsExtension => Extension is not null;

    private readonly bool isValid;

    private readonly ExtensionObject<ICommandProvider> _commandProvider;

    private readonly TaskScheduler _taskScheduler;

    private readonly ICommandProviderCache? _commandProviderCache;

    private readonly CommandProviderContext _providerContext;

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

    public CommandProviderWrapper(ICommandProvider provider, TaskScheduler mainThread)
    {
        // This ctor is only used for in-proc builtin commands. So the Unsafe!
        // calls are pretty dang safe actually.
        _commandProvider = new(provider);
        _taskScheduler = mainThread;

        // Hook the extension back into us
        ExtensionHost = new CommandPaletteHost(provider);
        _commandProvider.Unsafe!.InitializeWithHost(ExtensionHost);

        _commandProvider.Unsafe!.ItemsChanged += CommandProvider_ItemsChanged;

        isValid = true;
        Id = provider.Id;
        DisplayName = provider.DisplayName;
        Icon = new(provider.Icon);
        Icon.InitializeProperties();

        _providerContext = new() { ProviderId = ProviderId };

        // Note: explicitly not InitializeProperties()ing the settings here. If
        // we do that, then we'd regress GH #38321
        Settings = new(provider.Settings, this, _taskScheduler);

        Logger.LogDebug($"Initialized command provider {ProviderId}");
    }

    public CommandProviderWrapper(IExtensionWrapper extension, TaskScheduler mainThread, ICommandProviderCache commandProviderCache)
    {
        _taskScheduler = mainThread;
        _commandProviderCache = commandProviderCache;

        Extension = extension;
        ExtensionHost = new CommandPaletteHost(extension);
        _providerContext = new() { ProviderId = ProviderId };
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

    public async Task LoadTopLevelCommands(IServiceProvider serviceProvider, WeakReference<IPageContext> pageContext)
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

            // Load pinned commands from saved settings
            var pinnedCommands = LoadPinnedCommands(model, providerSettings);

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
            InitializeCommands(objects, serviceProvider, pageContext);

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
        WeakReference<IPageContext> pageContext)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;
        var state = serviceProvider.GetService<AppStateModel>()!;
        var providerSettings = GetProviderSettings(settings);

        var make = (ICommandItem? i, TopLevelType t) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), pageContext);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, t, ExtensionHost, _providerContext, settings, providerSettings, serviceProvider, i);
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

        if (objects.DockBands is not null)
        {
            List<TopLevelViewModel> bands = new();
            foreach (var b in objects.DockBands)
            {
                var bandVm = make(b, TopLevelType.DockBand);
                bands.Add(bandVm);
            }

            foreach (var c in TopLevelItems)
            {
                foreach (var pinnedId in settings.DockSettings.PinnedCommands)
                {
                    if (pinnedId == c.Id)
                    {
                        var bandModel = c.ToPinnedDockBandItem();
                        var bandVm = make(bandModel, TopLevelType.DockBand);
                        bands.Add(bandVm);
                        break;
                    }
                }
            }

            DockBandItems = bands.ToArray();
        }
    }

    private ICommandItem[] LoadPinnedCommands(ICommandProvider model, ProviderSettings providerSettings)
    {
        var pinnedItems = new List<ICommandItem>();

        if (model is ICommandProvider4 provider4)
        {
            foreach (var pinnedId in providerSettings.PinnedCommandIds)
            {
                try
                {
                    var commandItem = provider4.GetCommandItem(pinnedId);
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

    public CommandProviderContext GetProviderContext()
    {
        return _providerContext;
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

    internal void PinDockBand(TopLevelViewModel bandVm)
    {
        Logger.LogDebug($"CommandProviderWrapper.PinDockBand: {ProviderId} - {bandVm.Id}");

        var bands = this.DockBandItems.ToList();
        bands.Add(bandVm);
        this.DockBandItems = bands.ToArray();
        this.CommandsChanged?.Invoke(this, new ItemsChangedEventArgs());
    }
}
