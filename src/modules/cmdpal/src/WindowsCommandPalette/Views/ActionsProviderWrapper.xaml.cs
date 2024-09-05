// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CmdPal.Models;
using DeveloperCommandPalette;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;
using Windows.Win32;
using WindowsCommandPalette.BuiltinCommands;
using WindowsCommandPalette.BuiltinCommands.AllApps;

namespace WindowsCommandPalette.Views;

internal sealed class ActionsProviderWrapper
{
    public bool IsExtension => extensionWrapper != null;

    private readonly bool isValid;

    private ICommandProvider ActionProvider { get; }

    private readonly IExtensionWrapper? extensionWrapper;
    private IListItem[] _topLevelItems = [];

    public IListItem[] TopLevelItems => _topLevelItems;

    public ActionsProviderWrapper(ICommandProvider provider)
    {
        ActionProvider = provider;
        isValid = true;
    }

    public ActionsProviderWrapper(IExtensionWrapper extension)
    {
        extensionWrapper = extension;
        var extensionImpl = extension.GetExtensionObject();
        if (extensionImpl?.GetProvider(ProviderType.Commands) is not ICommandProvider provider)
        {
            throw new ArgumentException("extension didn't actually implement ICommandProvider");
        }

        ActionProvider = provider;
        isValid = true;
    }

    public async Task LoadTopLevelCommands()
    {
        if (!isValid)
        {
            return;
        }

        var t = new Task<IListItem[]>(() => ActionProvider.TopLevelCommands());
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

    public override bool Equals(object? obj) => obj is ActionsProviderWrapper wrapper && isValid == wrapper.isValid;

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
