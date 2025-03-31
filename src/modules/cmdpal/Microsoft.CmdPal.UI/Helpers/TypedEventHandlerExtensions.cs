// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Common.Deferred;
using Windows.Foundation;

// Pilfered from CommunityToolkit.WinUI.Deferred
namespace Microsoft.CmdPal.UI.Deferred;

/// <summary>
/// Extensions to <see cref="TypedEventHandler{TSender, TResult}"/> for Deferred Events.
/// </summary>
public static class TypedEventHandlerExtensions
{
    /// <summary>
    /// Use to invoke an async <see cref="TypedEventHandler{TSender, TResult}"/> using <see cref="DeferredEventArgs"/>.
    /// </summary>
    /// <typeparam name="S">Type of sender.</typeparam>
    /// <typeparam name="R"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="TypedEventHandler{TSender, TResult}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
#pragma warning disable CA1715 // Identifiers should have correct prefix
#pragma warning disable SA1314 // Type parameter names should begin with T
    public static Task InvokeAsync<S, R>(this TypedEventHandler<S, R> eventHandler, S sender, R eventArgs)
#pragma warning restore SA1314 // Type parameter names should begin with T
#pragma warning restore CA1715 // Identifiers should have correct prefix
        where R : DeferredEventArgs => InvokeAsync(eventHandler, sender, eventArgs, CancellationToken.None);

    /// <summary>
    /// Use to invoke an async <see cref="TypedEventHandler{TSender, TResult}"/> using <see cref="DeferredEventArgs"/> with a <see cref="CancellationToken"/>.
    /// </summary>
    /// <typeparam name="S">Type of sender.</typeparam>
    /// <typeparam name="R"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="TypedEventHandler{TSender, TResult}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> option.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
#pragma warning disable CA1715 // Identifiers should have correct prefix
#pragma warning disable SA1314 // Type parameter names should begin with T
    public static Task InvokeAsync<S, R>(this TypedEventHandler<S, R> eventHandler, S sender, R eventArgs, CancellationToken cancellationToken)
#pragma warning restore SA1314 // Type parameter names should begin with T
#pragma warning restore CA1715 // Identifiers should have correct prefix
        where R : DeferredEventArgs
    {
        if (eventHandler == null)
        {
            return Task.CompletedTask;
        }

        var tasks = eventHandler.GetInvocationList()
            .OfType<TypedEventHandler<S, R>>()
            .Select(invocationDelegate =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                invocationDelegate(sender, eventArgs);

#pragma warning disable CS0618 // Type or member is obsolete
                var deferral = eventArgs.GetCurrentDeferralAndReset();

                return deferral?.WaitForCompletion(cancellationToken) ?? Task.CompletedTask;
#pragma warning restore CS0618 // Type or member is obsolete
            })
            .ToArray();

        return Task.WhenAll(tasks);
    }
}
