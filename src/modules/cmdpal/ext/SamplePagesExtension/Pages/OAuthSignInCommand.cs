// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// Demonstrates the built-in Command Palette authorization flow. On invoke it
/// runs an OAuth 2.0 Authorization Code + PKCE sign-in through the host redirect
/// broker (via the Toolkit <see cref="OAuthClient"/>), optionally persists the
/// token in the Windows Credential Manager, and then asks the host to navigate to
/// a signed-in landing page.
/// </summary>
/// <remarks>
/// This sample is illustrative. Running it requires a real identity provider and
/// an interactive browser sign-in, so it cannot be exercised headlessly. The
/// defaults below target the Duende IdentityServer public demo, which is a
/// well-known, secretless, PKCE-friendly test provider. Swap the constants for
/// your own registered public client to bring your own provider.
/// </remarks>
internal sealed partial class OAuthSignInCommand : InvokableCommand
{
    // Demo identity provider values. These are safe to ship because the flow uses
    // PKCE with a public client (there is no client secret). Replace them with your
    // own registered client to target a different provider.
    private const string DemoClientId = "interactive.public";
    private const string DemoAuthorizationEndpoint = "https://demo.duendesoftware.com/connect/authorize";
    private const string DemoTokenEndpoint = "https://demo.duendesoftware.com/connect/token";
    private const string DemoScopes = "openid profile";

    // A distinct namespace so the optional token store does not collide with other
    // extensions that also persist tokens.
    private const string TokenStoreNamespace = "SamplePagesExtension.OAuthSample";
    private const string TokenStoreKey = "demo.duende";

    public OAuthSignInCommand()
    {
        Name = "Sign in";
        Icon = new IconInfo("\uE8FA"); // Permissions
    }

    public override ICommandResult Invoke()
    {
        // Older Command Palette hosts do not implement the authorization broker.
        // Fail politely instead of throwing NotSupportedException at the user.
        if (!ExtensionHost.SupportsAuthorization)
        {
            return CommandResult.ShowToast(
                "This build of Command Palette does not support sign-in. Update Command Palette to try the built-in authorization flow.");
        }

        // The interactive flow waits for a browser round trip, so run it off the
        // invoke thread. On success the host drives navigation to the signed-in
        // page; on failure we surface a short status toast.
        _ = SignInAsync();

        // Keep the entry page open while the browser sign-in happens.
        return CommandResult.KeepOpen();
    }

    private static async Task SignInAsync()
    {
        try
        {
            var client = new OAuthClient
            {
                ClientId = DemoClientId,
                AuthorizationEndpoint = DemoAuthorizationEndpoint,
                TokenEndpoint = DemoTokenEndpoint,
                Scopes = DemoScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                RedirectKind = AuthorizationRedirectKind.Loopback,
                DisplayName = "Command Palette OAuth sample",
            };

            // Runs PKCE generation, the host-brokered redirect, and the token
            // exchange. Everything sensitive stays inside this extension process.
            var token = await client.AuthorizeAsync().ConfigureAwait(false);

            TryPersist(token);

            // Phase 3 host-driven navigation: move Command Palette to a signed-in
            // landing page that shows non-sensitive facts about the session.
            await ExtensionHost.GoToPageAsync(new SampleSignedInPage(token), NavigationMode.Push).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never surface the authorization code, token, or PKCE verifier. Keep
            // the user-facing message generic and only log the exception type.
            new ToastStatusMessage("Sign-in did not complete. See the Command Palette logs for details.").Show();
            ExtensionHost.LogMessage($"OAuth sample sign-in failed: {ex.GetType().Name}");
        }
    }

    private static void TryPersist(OAuthToken token)
    {
        // Persisting the token is optional. Guard it so a storage failure (for
        // example an oversized token that exceeds the vault limit) does not break
        // the demo.
        try
        {
            var store = new CredentialManagerTokenStore(TokenStoreNamespace);
            store.Save(TokenStoreKey, token);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"OAuth sample could not persist the token: {ex.GetType().Name}");
        }
    }
}
