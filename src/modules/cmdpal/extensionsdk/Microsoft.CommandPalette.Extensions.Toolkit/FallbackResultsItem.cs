// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a fallback that returns a set of query results.
/// </summary>
public abstract partial class FallbackResultsItem : FallbackCommandItem, IFallbackCommandItem3, IFallbackHandler2
{
    protected FallbackResultsItem(string displayTitle, string id)
        : base(displayTitle, id)
    {
    }

    public IFallbackHandler2 QueryHandler => this;

    string IFallbackCommandItem3.Name => string.Empty;

    string IFallbackCommandItem3.TitleTemplate => string.Empty;

    string IFallbackCommandItem3.SubtitleTemplate => string.Empty;

    HostMatchKind IFallbackCommandItem3.MatchKind => HostMatchKind.None;

    string IFallbackCommandItem3.MatchValue => string.Empty;

    ICommand IFallbackCommandItem3.CreateCommand(IFallbackCommandInvocationArgs args) => Command!;

    public override void UpdateQuery(string query)
    {
    }

    public virtual IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> QueryAsync(IFallbackQueryArgs args)
    {
        return AsyncInfo.Run<IFallbackCommandResult, IFallbackCommandResult>(
            (cancellationToken, progress) => QueryAsync(args, cancellationToken, progress));
    }

    protected virtual Task<IFallbackCommandResult> QueryAsync(
        IFallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        return Task.FromException<IFallbackCommandResult>(new NotSupportedException("Override the cancellable QueryAsync method."));
    }
}
