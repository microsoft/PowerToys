// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A ready-to-use <see cref="IAuthorizationRequest"/> implementation. The host
/// injects <c>redirect_uri</c> and <c>state</c>, so do not put those in
/// <see cref="Parameters"/>.
/// </summary>
public partial class AuthorizationRequest : IAuthorizationRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

    public AuthorizationRedirectKind RedirectKind { get; set; } = AuthorizationRedirectKind.Loopback;

    public int TimeoutSeconds { get; set; }
}
