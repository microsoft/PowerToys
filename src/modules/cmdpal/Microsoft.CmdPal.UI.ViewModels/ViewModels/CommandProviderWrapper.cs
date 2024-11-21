// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandProviderWrapper
{
    public bool IsExtension => extensionWrapper != null;

    private readonly bool isValid;

    private readonly ICommandProvider _commandProvider;

    private readonly IExtensionWrapper? extensionWrapper;
    private ICommandItem[] _topLevelItems = [];

    public ICommandItem[] TopLevelItems => _topLevelItems;

    public CommandProviderWrapper(ICommandProvider provider)
    {
        _commandProvider = provider;
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

        _commandProvider = provider;
        isValid = true;
    }

    public async Task LoadTopLevelCommands()
    {
        if (!isValid)
        {
            return;
        }

        var t = new Task<ICommandItem[]>(() => _commandProvider.TopLevelCommands());
        t.Start();
        var commands = await t.ConfigureAwait(false);

        // On a BG thread here
        if (commands != null)
        {
            _topLevelItems = commands;
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
}
