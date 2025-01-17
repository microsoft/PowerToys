// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandProviderWrapper
{
    public bool IsExtension => extensionWrapper != null;

    private readonly bool isValid;

    private readonly ICommandProvider _commandProvider;

    private readonly IExtensionWrapper? extensionWrapper;

    public ICommandItem[] TopLevelItems { get; private set; } = [];

    public IFallbackCommandItem[] FallbackItems { get; private set; } = [];

    public event TypedEventHandler<CommandProviderWrapper, ItemsChangedEventArgs>? CommandsChanged;

    public string Id { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public string ProviderId => $"{extensionWrapper?.PackageFamilyName ?? string.Empty}/{Id}";

    public IExtensionWrapper? Extension => extensionWrapper;

    public CommandProviderWrapper(ICommandProvider provider)
    {
        _commandProvider = provider;
        _commandProvider.ItemsChanged += CommandProvider_ItemsChanged;

        isValid = true;
        Id = provider.Id;
        DisplayName = provider.DisplayName;
        Icon = new(provider.Icon);
        Icon.InitializeProperties();
    }

    public CommandProviderWrapper(IExtensionWrapper extension)
    {
        extensionWrapper = extension;
        if (!extensionWrapper.IsRunning())
        {
            throw new ArgumentException("You forgot to start the extension. This is a coding error - make sure to call StartExtensionAsync");
        }

        var extensionImpl = extension.GetExtensionObject();
        var providerObject = extensionImpl?.GetProvider(ProviderType.Commands);
        if (providerObject is not ICommandProvider provider)
        {
            throw new ArgumentException("extension didn't actually implement ICommandProvider");
        }

        _commandProvider = provider;

        try
        {
            _commandProvider.ItemsChanged += CommandProvider_ItemsChanged;
        }
        catch
        {
        }

        isValid = true;
    }

    public async Task LoadTopLevelCommands()
    {
        if (!isValid)
        {
            return;
        }

        var t = new Task<ICommandItem[]>(_commandProvider.TopLevelCommands);
        t.Start();
        var commands = await t.ConfigureAwait(false);

        // On a BG thread here
        var fallbacks = _commandProvider.FallbackCommands();

        Id = _commandProvider.Id;
        DisplayName = _commandProvider.DisplayName;
        Icon = new(_commandProvider.Icon);
        Icon.InitializeProperties();

        if (commands != null)
        {
            TopLevelItems = commands;
        }

        if (fallbacks != null)
        {
            FallbackItems = fallbacks;
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

    private void CommandProvider_ItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        // We don't want to handle this ourselves - we want the
        // TopLevelCommandManager to know about this, so they can remove
        // our old commands from their own list.
        //
        // In handling this, a call will be made to `LoadTopLevelCommands` to
        // retrieve the new items.
        this.CommandsChanged?.Invoke(this, args);
    }
}
