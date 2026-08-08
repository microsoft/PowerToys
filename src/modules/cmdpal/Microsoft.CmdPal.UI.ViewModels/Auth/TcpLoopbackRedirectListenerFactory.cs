// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Sockets;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// Default <see cref="ILoopbackRedirectListenerFactory"/>. Produces listeners
/// backed by a raw <see cref="TcpListener"/> so no HTTP.SYS URL ACL (and thus no
/// elevation) is required inside the packaged Command Palette process.
/// </summary>
public sealed partial class TcpLoopbackRedirectListenerFactory : ILoopbackRedirectListenerFactory
{
    public ILoopbackRedirectListener Create() => new TcpLoopbackRedirectListener();
}
