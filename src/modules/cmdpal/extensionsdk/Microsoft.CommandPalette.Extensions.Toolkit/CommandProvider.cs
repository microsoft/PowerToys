// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class CommandProvider :
    ICommandProvider,
    ICommandProvider2,
    ICommandProvider3,
    ICommandProvider4
{
    public virtual string Id { get; protected set; } = string.Empty;

    public virtual string DisplayName { get; protected set; } = string.Empty;

    public virtual IconInfo Icon { get; protected set; } = new IconInfo();

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public abstract ICommandItem[] TopLevelCommands();

    public virtual IFallbackCommandItem[]? FallbackCommands() => null;

    public virtual ICommand? GetCommand(string id) => null;

    public virtual ICommandItem? GetCommandItem(string id) => null;

    public virtual ICommandSettings? Settings { get; protected set; }

    public virtual bool Frozen { get; protected set; } = true;

    IIconInfo ICommandProvider.Icon => Icon;

    public virtual void InitializeWithHost(IExtensionHost host) => ExtensionHost.Initialize(host);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public virtual void Dispose()
    {
    }
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    protected void RaiseItemsChanged(int totalItems = -1)
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

    /// <summary>
    /// Get the dock bands provided by this command provider. Dock bands are
    /// strips of items that appear on various UI surfaces in CmdPal, such as a
    /// toolbar. Each ICommandItem returned from this method will be treated as
    /// one atomic band by cmdpal.
    ///
    /// If the command on an item here is a
    /// IListPage, then cmdpal will render all of the items on that page as one
    /// band. You can use this to create complex bands with multiple buttons.
    /// </summary>
    public virtual ICommandItem[]? GetDockBands()
    {
        return null;
    }

    /// <summary>
    /// This is used to manually populate the WinRT type cache in CmdPal with
    /// any interfaces that might not follow a straight linear path of requires.
    ///
    /// You don't need to call this as an extension author.
    /// </summary>
    /// <returns>an array of objects that implement all the leaf interfaces we support</returns>
    public object[] GetApiExtensionStubs()
    {
        return
        [
            new SupportCommandsWithProperties(),

            // The host receives fallbacks as IFallbackCommandItem, then asks each one
            // for IFallbackCommandItem3. It reaches the newer interfaces only by that
            // question, so it must know them first. Without these stubs the question
            // gets the answer "no" and every v2 fallback drops back to v1 behavior.
            new SupportFallbackCommandItem2(),
            new SupportFallbackCommandItem3(),
            new SupportFallbackResultQueries(),
            new FallbackQueryResult(string.Empty, string.Empty, []),
        ];
    }

    /// <summary>
    /// A stub class which implements IExtendedAttributesProvider. Just marshalling this
    /// across the ABI will be enough for CmdPal to store IExtendedAttributesProvider in
    /// its type cache.
    /// </summary>
    private sealed partial class SupportCommandsWithProperties : IExtendedAttributesProvider
    {
        public IDictionary<string, object>? GetProperties() => null;
    }

    /// <summary>
    /// Implements only IFallbackCommandItem2. CsWinRT does not register required
    /// interfaces when the host checks an IFallbackCommandItem3 stub, so this interface
    /// needs its own stub.
    /// </summary>
    private sealed partial class SupportFallbackCommandItem2 : IFallbackCommandItem2
    {
        public string Title => string.Empty;

        public string Subtitle => string.Empty;

        public IIconInfo Icon => null!;

        public ICommand Command => null!;

        public IContextItem[] MoreCommands => [];

        public IFallbackHandler FallbackHandler => null!;

        public string DisplayTitle => string.Empty;

        public string Id => string.Empty;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add { }
            remove { }
        }
    }

    /// <summary>
    /// Implements only IFallbackCommandItem3, so that CmdPal can add that interface
    /// to its type cache before it receives real fallback objects.
    /// </summary>
    private sealed partial class SupportFallbackCommandItem3 : IFallbackCommandItem3
    {
        public string Title => string.Empty;

        public string Subtitle => string.Empty;

        public IIconInfo Icon => null!;

        public ICommand Command => null!;

        public IContextItem[] MoreCommands => [];

        public IFallbackHandler FallbackHandler => null!;

        public string DisplayTitle => string.Empty;

        public string Id => string.Empty;

        public FallbackCommandMode Mode => FallbackCommandMode.Active;

        public string Name => string.Empty;

        public string TitleTemplate => string.Empty;

        public string SubtitleTemplate => string.Empty;

        public HostMatchKind MatchKind => HostMatchKind.None;

        public string MatchValue => string.Empty;

        public OptionalUInt32 SuggestedQueryDelayMilliseconds => default;

        public OptionalUInt32 SuggestedMinQueryLength => default;

        public IFallbackHandler2 QueryHandler => null!;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add { }
            remove { }
        }

        public ICommand CreateCommand(IFallbackCommandInvocationArgs args) => null!;
    }

    /// <summary>
    /// A stub class which implements IFallbackHandler2, so that CmdPal can read the
    /// QueryHandler of a fallback that returns results.
    /// </summary>
    private sealed partial class SupportFallbackResultQueries : IFallbackHandler2
    {
        public void UpdateQuery(string query)
        {
        }

        public IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> QueryAsync(IFallbackQueryArgs args)
            => throw new NotSupportedException("This type only populates the type cache.");
    }
}
