// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandProviderWrapper
{
    public bool IsExtension => Extension != null;

    private readonly bool isValid;

    private readonly ExtensionObject<ICommandProvider> _commandProvider;

    private readonly TaskScheduler _taskScheduler;

    public TopLevelViewModel[] TopLevelItems { get; private set; } = [];

    public TopLevelViewModel[] FallbackItems { get; private set; } = [];

    public string DisplayName { get; private set; } = string.Empty;

    public IExtensionWrapper? Extension { get; }

    public CommandPaletteHost ExtensionHost { get; private set; }

    public event TypedEventHandler<CommandProviderWrapper, IItemsChangedEventArgs>? CommandsChanged;

    public string Id { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public CommandSettingsViewModel? Settings { get; private set; }

    public string ProviderId
    {
        get
        {
            return string.IsNullOrEmpty(Extension?.ExtensionUniqueId) ? Id : Extension.ExtensionUniqueId;
        }
    }

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
        Settings = new(provider.Settings, this, _taskScheduler);
        Settings.InitializeProperties();

        Logger.LogDebug($"Initialized command provider {ProviderId}");
    }

    public CommandProviderWrapper(IExtensionWrapper extension, TaskScheduler mainThread)
    {
        _taskScheduler = mainThread;
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

    public async Task LoadTopLevelCommands(IServiceProvider serviceProvider, WeakReference<IPageContext> pageContext)
    {
        if (!isValid)
        {
            return;
        }

        var settings = serviceProvider.GetService<SettingsModel>()!;

        if (!GetProviderSettings(settings).IsEnabled)
        {
            return;
        }

        ICommandItem[]? commands = null;
        IFallbackCommandItem[]? fallbacks = null;

        try
        {
            var model = _commandProvider.Unsafe!;

            Task<ICommandItem[]> t = new(model.TopLevelCommands);
            t.Start();
            commands = await t.ConfigureAwait(false);

            // On a BG thread here
            fallbacks = model.FallbackCommands();

            Id = model.Id;
            DisplayName = model.DisplayName;
            Icon = new(model.Icon);
            Icon.InitializeProperties();

            Settings = new(model.Settings, this, _taskScheduler);
            Settings.InitializeProperties();

            InitializeCommands(commands, fallbacks, serviceProvider, pageContext);

            Logger.LogDebug($"Loaded commands from {DisplayName} ({ProviderId})");
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load commands from extension");
            Logger.LogError($"Extension was {Extension!.PackageFamilyName}");
            Logger.LogError(e.ToString());
        }
    }

    private void InitializeCommands(ICommandItem[] commands, IFallbackCommandItem[] fallbacks, IServiceProvider serviceProvider, WeakReference<IPageContext> pageContext)
    {
        var settings = serviceProvider.GetService<SettingsModel>()!;

        Func<ICommandItem?, bool, TopLevelViewModel> makeAndAdd = (ICommandItem? i, bool fallback) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), pageContext);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, fallback, ExtensionHost, ProviderId, settings, serviceProvider);

            topLevelViewModel.ItemViewModel.SlowInitializeProperties();

            return topLevelViewModel;
        };
        if (commands != null)
        {
            TopLevelItems = commands
                .Select(c => makeAndAdd(c, false))
                .ToArray();
        }

        if (fallbacks != null)
        {
            FallbackItems = fallbacks
                .Select(c => makeAndAdd(c, true))
                .ToArray();
        }
    }

    /* This is a View/ExtensionHost piece
     * public void AllowSetForeground(bool allow)
    {
        if (!IsExtension)
        {
            return;
        }

        var iextn = extensionWrapper?.GetExtensionObject();
        unsafe
        {
            PInvoke.CoAllowSetForegroundWindow(iextn);
        }
    }*/

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
}
