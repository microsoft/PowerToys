// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A token set returned from an OAuth 2.0 token endpoint.
/// </summary>
public sealed partial class OAuthToken
{
    public string AccessToken { get; init; } = string.Empty;

    public string? RefreshToken { get; init; }

    public string? TokenType { get; init; }

    public string? Scope { get; init; }

    public string? IdToken { get; init; }

    /// <summary>
    /// Absolute expiry, computed from the token endpoint's <c>expires_in</c>
    /// (relative seconds) at the moment the response was received. Null when the
    /// provider did not return an expiry.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// True when the token has an expiry and it is within <paramref name="skew"/>
    /// of now (default 60s of clock skew), so the caller should refresh.
    /// </summary>
    public bool IsExpired(TimeSpan? skew = null) =>
        ExpiresAt is DateTimeOffset expiry &&
        DateTimeOffset.UtcNow >= expiry - (skew ?? TimeSpan.FromSeconds(60));
}
