// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.Extensions.Helpers;

public abstract partial class CommandProvider : ICommandProvider
{
    private string _id = string.Empty;

    private string _displayName = string.Empty;

    private IconInfo _icon = new(string.Empty);

    private ICommandSettings? _settings;

    public string Id { get => _id; protected set => _id = value; }

    public string DisplayName { get => _displayName; protected set => _displayName = value; }

    public IconInfo Icon { get => _icon; protected set => _icon = value; }

    public event TypedEventHandler<object, ItemsChangedEventArgs>? ItemsChanged;

    public abstract ICommandItem[] TopLevelCommands();

    public virtual IFallbackCommandItem[]? FallbackCommands()
    {
        return null;
    }

    public virtual ICommand? GetCommand(string id)
    {
        return null;
    }

    public ICommandSettings? Settings { get => _settings; protected set => _settings = value; }

    public bool Frozen { get; protected set; } = true;

    public void InitializeWithHost(IExtensionHost host)
    {
        ExtensionHost.Initialize(host);
    }

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
