// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class CommandProvider : ICommandProvider
{
    public string Id { get; protected set; } = string.Empty;

    public string DisplayName { get; protected set; } = string.Empty;

    public IconInfo Icon { get; protected set; } = new IconInfo();

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public abstract ICommandItem[] TopLevelCommands();

    public virtual IFallbackCommandItem[]? FallbackCommands() => null;

    public virtual ICommand? GetCommand(string id) => null;

    public ICommandSettings? Settings { get; protected set; }

    public bool Frozen { get; protected set; } = true;

    IIconInfo ICommandProvider.Icon => Icon;

    public virtual void InitializeWithHost(IExtensionHost host) => ExtensionHost.Initialize(host);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose()
    {
    }
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    protected void RaiseItemsChanged(int totalItems)
    {
        try
        {
            // TODO #181 - This is the same thing that BaseObservable has to deal with.
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}
