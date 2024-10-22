// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;
using Windows.Win32;

namespace WindowsCommandPalette.Views;

public sealed class CommandProviderWrapper
{
    public bool IsExtension => extensionWrapper != null;

    private readonly bool isValid;

    private ICommandProvider CommandProvider { get; }

    private readonly IExtensionWrapper? extensionWrapper;
    private IListItem[] _topLevelItems = [];

    public IListItem[] TopLevelItems => _topLevelItems;

    public CommandProviderWrapper(ICommandProvider provider)
    {
        CommandProvider = provider;
        isValid = true;
    }

    public CommandProviderWrapper(IExtensionWrapper extension)
    {
        extensionWrapper = extension;
        var extensionImpl = extension.GetExtensionObject();
        if (extensionImpl?.GetProvider(ProviderType.Commands) is not ICommandProvider provider)
        {
            throw new ArgumentException("extension didn't actually implement ICommandProvider");
        }

        CommandProvider = provider;
        isValid = true;
    }

    public async Task LoadTopLevelCommands()
    {
        if (!isValid)
        {
            return;
        }

        var t = new Task<IListItem[]>(() => CommandProvider.TopLevelCommands());
        t.Start();
        var commands = await t.ConfigureAwait(false);

        // On a BG thread here
        if (commands != null)
        {
            _topLevelItems = commands;
        }
    }

    public void AllowSetForeground(bool allow)
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
    }

    public override bool Equals(object? obj) => obj is CommandProviderWrapper wrapper && isValid == wrapper.isValid;

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
