// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Security.Credentials;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// An <see cref="ITokenStore"/> backed by the Windows Credential Manager (via
/// <see cref="PasswordVault"/>). Tokens are encrypted at rest per-user. Requires
/// the extension to run as a packaged app (Command Palette extensions do).
/// </summary>
/// <remarks>
/// The vault imposes a size limit on the stored secret (a few KB), which is fine
/// for typical access/refresh tokens but may be exceeded by very large JWTs.
/// </remarks>
public sealed partial class CredentialManagerTokenStore : ITokenStore
{
    private const string ResourcePrefix = "Microsoft.CmdPal.OAuth:";

    private readonly string _resource;

    /// <summary>
    /// Create a store. Use a distinct <paramref name="ns"/> per provider/account
    /// namespace so keys don't collide across extensions.
    /// </summary>
    public CredentialManagerTokenStore(string ns = "default")
    {
        _resource = ResourcePrefix + ns;
    }

    public OAuthToken? Retrieve(string key)
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(_resource, key);
            credential.RetrievePassword();
            return OAuthTokenSerialization.Deserialize(credential.Password);
        }
        catch
        {
            // PasswordVault throws when the entry does not exist; treat as "no token".
            return null;
        }
    }

    public void Save(string key, OAuthToken token)
    {
        Remove(key);
        var vault = new PasswordVault();
        var json = OAuthTokenSerialization.Serialize(token);
        vault.Add(new PasswordCredential(_resource, key, json));
    }

    public void Remove(string key)
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(_resource, key);
            vault.Remove(credential);
        }
        catch
        {
            // Nothing stored under this key; nothing to remove.
        }
    }
}
