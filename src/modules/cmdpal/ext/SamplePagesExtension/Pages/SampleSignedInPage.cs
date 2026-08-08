// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// The landing page shown after a successful demo sign-in. It renders a friendly
/// confirmation and a few non-sensitive facts about the session. It never renders
/// the raw access token, refresh token, or id token.
/// </summary>
internal sealed partial class SampleSignedInPage : ContentPage
{
    private readonly string _markdown;

    public SampleSignedInPage(OAuthToken token)
    {
        Name = "Signed in";
        Title = "You are signed in";
        Icon = new IconInfo("\uE73E"); // CheckMark

        _markdown = BuildMarkdown(token);
    }

    public override IContent[] GetContent() => [new MarkdownContent(_markdown)];

    private static string BuildMarkdown(OAuthToken token)
    {
        // Only surface non-sensitive metadata. The access, refresh, and id tokens
        // are deliberately omitted.
        var tokenType = string.IsNullOrEmpty(token.TokenType) ? "(not reported)" : token.TokenType;
        var scope = string.IsNullOrEmpty(token.Scope) ? "(not reported)" : token.Scope;
        var expiry = token.ExpiresAt is { } expiresAt
            ? expiresAt.ToLocalTime().ToString("f", CultureInfo.CurrentCulture)
            : "(no expiry reported)";
        var hasRefresh = string.IsNullOrEmpty(token.RefreshToken) ? "No" : "Yes";
        var hasIdToken = string.IsNullOrEmpty(token.IdToken) ? "No" : "Yes";

        var builder = new StringBuilder();
        builder.AppendLine("# You are signed in");
        builder.AppendLine();
        builder.AppendLine("Command Palette brokered the browser redirect and this extension exchanged the authorization code for tokens using PKCE. The tokens stayed inside the extension process the whole time.");
        builder.AppendLine();
        builder.AppendLine("## Session facts");
        builder.AppendLine();
        builder.AppendLine("| Fact | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine(FormattableString.Invariant($"| Token type | {tokenType} |"));
        builder.AppendLine(FormattableString.Invariant($"| Granted scope | {scope} |"));
        builder.AppendLine(FormattableString.Invariant($"| Expires | {expiry} |"));
        builder.AppendLine(FormattableString.Invariant($"| Refresh token received | {hasRefresh} |"));
        builder.AppendLine(FormattableString.Invariant($"| Id token received | {hasIdToken} |"));
        builder.AppendLine();
        builder.AppendLine("The raw access token is never shown here. If you opted in, it was saved to the Windows Credential Manager for this extension only.");

        return builder.ToString();
    }
}
