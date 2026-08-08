// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// Entry page for the OAuth sign-in sample. It shows the current state and offers
/// a sign-in action. When the connected Command Palette host is too old to support
/// the authorization broker, it shows a graceful message instead of offering a
/// flow that would fail.
/// </summary>
internal sealed partial class SampleOAuthPage : ListPage
{
    private const string OverviewMarkdown =
        "This sample runs an OAuth 2.0 Authorization Code flow with PKCE. Command Palette opens the browser, "
        + "allocates a loopback redirect, validates the state parameter, and hands the captured redirect back to "
        + "this extension. The extension exchanges the code for tokens itself, so no secret or token ever reaches "
        + "the host.\n\n"
        + "The default target is the Duende IdentityServer public demo, which is a secretless test provider. "
        + "Running it opens a real browser and needs an interactive sign-in.";

    public SampleOAuthPage()
    {
        Name = "OAuth sign-in";
        Title = "Sample: OAuth sign-in";
        Icon = new IconInfo("\uE8FA"); // Permissions
    }

    public override IListItem[] GetItems()
    {
        if (!ExtensionHost.SupportsAuthorization)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Sign-in is not available",
                    Subtitle = "This build of Command Palette does not support the built-in authorization flow. Update Command Palette to try it.",
                    Icon = new IconInfo("\uE7BA"), // Warning
                },
            ];
        }

        return
        [
            new ListItem(new OAuthSignInCommand())
            {
                Title = "Sign in with the demo identity provider",
                Subtitle = "Runs Authorization Code + PKCE against demo.duendesoftware.com, then navigates to a signed-in page",
                Details = new Details()
                {
                    Title = "About this sample",
                    Body = OverviewMarkdown,
                },
            },
        ];
    }
}
