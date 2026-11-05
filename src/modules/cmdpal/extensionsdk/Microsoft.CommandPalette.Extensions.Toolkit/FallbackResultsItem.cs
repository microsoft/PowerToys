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

    /// <summary>
    /// Unused. Extensions using the FallbackResultsItem should implement QueryAsync instead.
    /// </summary>
    public override void UpdateQuery(string query)
    {
    }

    public virtual IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> QueryAsync(IFallbackQueryArgs args)
    {
        try
        {
            // NOTE: IFallbackQueryArgs is an object that's owned by the host, and
            // marshalled-by-reference across the process boundary.
            //
            // Copy the values we need before returning from this method.
            var queryArgs = new FallbackQueryArgs(args.Query, args.QueryId, args.RequestedItemCount, args.LanguageTags);
            return AsyncInfo.Run<IFallbackCommandResult, IFallbackCommandResult>(
                (cancellationToken, progress) => QueryAsync(queryArgs, cancellationToken, progress));
        }
        catch (Exception ex)
        {
            return AsyncInfo.Run<IFallbackCommandResult, IFallbackCommandResult>(
                (cancellationToken, progress) => Task.FromException<IFallbackCommandResult>(ex));
        }
    }

    /// <summary>
    /// Called when the host sends an extension a new top-level search query.
    /// Extensions with top-level fallback results should implement this method
    /// to return a set of relevant results.
    /// </summary>
    /// <param name="args">Contains the query information, like what the search
    /// string was.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the
    /// asynchronous operation. The host (Command Palette) may cancel this
    /// operation, which will be notified through this token.</param>
    /// <param name="progress">Used to report progress to the host.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous
    /// operation.</returns>
    protected abstract Task<IFallbackCommandResult> QueryAsync(
        FallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress);
}
