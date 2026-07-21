// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Thrown when an OAuth flow fails: the provider returned an error, the redirect
/// carried an <c>error</c> parameter, the flow was canceled or timed out, or the
/// token exchange failed.
/// </summary>
public sealed class OAuthException : Exception
{
    /// <summary>
    /// The OAuth <c>error</c> code when one was returned (e.g.
    /// <c>access_denied</c>, <c>invalid_grant</c>), otherwise null.
    /// </summary>
    public string? ErrorCode { get; }

    public OAuthException(string message)
        : base(message)
    {
    }

    public OAuthException(string message, string? errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public OAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
