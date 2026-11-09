// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Helpers for delivering notifications independently to each subscriber.
/// </summary>
public static class EventHelpers
{
    private const int RpcDisconnectedHResult = unchecked((int)0x80010108); // RPC_E_DISCONNECTED
    private const int RpcServerUnavailableHResult = unchecked((int)0x800706BA); // HRESULT_FROM_WIN32(RPC_S_SERVER_UNAVAILABLE)

    /// <summary>
    /// Raises an event without allowing one subscriber's failure to block the others.
    /// </summary>
    /// <param name="handlers">The subscribers to notify.</param>
    /// <param name="sender">The event source.</param>
    /// <param name="args">The event arguments.</param>
    /// <param name="unsubscribe">
    /// Optional callback to remove disconnected recipients. Omit when the publisher
    /// manages subscription lifetime separately.
    /// </param>
    /// <typeparam name="TEventArgs">The event argument type.</typeparam>
    public static void Raise<TEventArgs>(
        TypedEventHandler<object, TEventArgs>? handlers,
        object sender,
        TEventArgs args,
        Action<TypedEventHandler<object, TEventArgs>>? unsubscribe = null)
    {
        // A callback to a previous host must not prevent delivery to the current host.
        foreach (TypedEventHandler<object, TEventArgs> handler in Delegate.EnumerateInvocationList(handlers))
        {
            try
            {
                handler(sender, args);
            }
            catch (Exception ex) when (ex.HResult is RpcDisconnectedHResult or RpcServerUnavailableHResult)
            {
                // RPC_E_DISCONNECTED and RPC_S_SERVER_UNAVAILABLE identify dead recipients.
                // Let the publisher decide whether to remove them.
                // https://devblogs.microsoft.com/oldnewthing/20190521-00/?p=102505
                unsubscribe?.Invoke(handler);
            }
            catch
            {
                // Preserve the existing exception policy, but continue with the other subscribers.
            }
        }
    }
}
