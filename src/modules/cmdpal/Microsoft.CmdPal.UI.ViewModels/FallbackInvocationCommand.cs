// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class FallbackInvocationCommand : ICommand, IFallbackInvocationContext
{
    private readonly IFallbackCommandItem3 _fallback;
    private readonly IFallbackCommandInvocationArgs _args;
    private readonly FallbackQueryContext _queryContext;

    internal FallbackInvocationCommand(
        IFallbackCommandItem3 fallback,
        TopLevelViewModel source,
        IFallbackCommandInvocationArgs args,
        CancellationToken queryToken)
    {
        _fallback = fallback;
        _args = args;
        _queryContext = new(source.ExtensionHost, source.ProviderContext, source, queryToken);
        Name = fallback.Name;
        Id = $"{fallback.Id}.placeholder.{args.QueryId}";
        Icon = fallback.Icon;
    }

    public string Name { get; }

    public string Id { get; }

    public IIconInfo Icon { get; }

    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
    {
        add { }
        remove { }
    }

    public AppExtensionHost ExtensionHost => _queryContext.ExtensionHost;

    public ICommandProviderContext ProviderContext => _queryContext.ProviderContext;

    public object InvocationContext => _queryContext.InvocationContext;

    public ICommand? ResolveCommand(ICommand requestedCommand)
    {
        return _queryContext.CanInvoke ? _fallback.CreateCommand(_args) : null;
    }
}
