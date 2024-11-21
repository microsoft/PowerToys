// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private ICommandItem[] _topLevelItems = [];
    private IFallbackCommandItem[] _topLevelFallbacks = [];

    public IEnumerable<ICommandItem> TopLevelItems => _topLevelItems.Concat(_topLevelFallbacks);

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

        // Hook the extension back into us
        CommandProvider.InitializeWithHost(CommandPaletteHost.Instance);

        isValid = true;
    }

    public async Task LoadTopLevelCommands()
    {
        if (!isValid)
        {
            return;
        }

        var t = new Task<(ICommandItem[], IFallbackCommandItem[])>(() =>
        {
            try
            {
                return (CommandProvider.TopLevelCommands(), CommandProvider.FallbackCommands());
            }
            catch (COMException e)
            {
                if (extensionWrapper != null)
                {
                    Debug.WriteLine($"Error loading commands from {extensionWrapper.ExtensionDisplayName}", "error");
                }

                Debug.WriteLine(e.ToString(), "error");
            }

            return ([], []);
        });
        t.Start();
        var (commands, fallbacks) = await t.ConfigureAwait(false);

        // On a BG thread here
        if (commands != null)
        {
            _topLevelItems = commands;
        }

        if (fallbacks != null)
        {
            _topLevelFallbacks = fallbacks;
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
