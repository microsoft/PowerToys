// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// A single-use loopback redirect target for an authorization flow. The broker
/// allocates one of these per <see cref="AuthorizationRedirectKind.Loopback"/>
/// request, hands the extension the resulting <see cref="RedirectUri"/>, and
/// awaits the single browser redirect that carries the provider's response.
/// </summary>
public interface ILoopbackRedirectListener : IDisposable
{
    /// <summary>
    /// The <c>redirect_uri</c> to advertise to the identity provider, e.g.
    /// <c>http://127.0.0.1:{port}/</c>. Bound to the loopback interface only.
    /// </summary>
    string RedirectUri { get; }

    /// <summary>
    /// Wait for exactly one browser redirect to <see cref="RedirectUri"/> and
    /// return its query parameters. Requests to any other path are answered and
    /// ignored so the listener keeps waiting for the real callback.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> WaitForRedirectAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Creates <see cref="ILoopbackRedirectListener"/> instances. Abstracted so unit
/// tests can substitute a listener that resolves a canned redirect without
/// binding a socket.
/// </summary>
public interface ILoopbackRedirectListenerFactory
{
    ILoopbackRedirectListener Create();
}
