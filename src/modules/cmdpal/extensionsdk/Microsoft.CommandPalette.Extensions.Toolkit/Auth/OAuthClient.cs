// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A turnkey OAuth 2.0 Authorization Code + PKCE client for Command Palette
/// extensions. It generates the PKCE verifier/challenge, asks the Command Palette
/// host to run the interactive redirect (browser + capture), and then exchanges
/// the authorization code for tokens at the provider's token endpoint. All
/// secrets (PKCE verifier, tokens) stay inside the extension's process; Command
/// Palette only brokers the browser redirect.
/// </summary>
public sealed partial class OAuthClient
{
    private static readonly HttpClient SharedHttpClient = new();

    /// <summary>The OAuth client identifier (public client; no secret).</summary>
    public required string ClientId { get; init; }

    /// <summary>The provider's authorization endpoint URL.</summary>
    public required string AuthorizationEndpoint { get; init; }

    /// <summary>The provider's token endpoint URL.</summary>
    public required string TokenEndpoint { get; init; }

    /// <summary>Scopes to request. Joined with spaces into the <c>scope</c> parameter.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>Which redirect mechanism Command Palette should host.</summary>
    public AuthorizationRedirectKind RedirectKind { get; init; } = AuthorizationRedirectKind.Loopback;

    /// <summary>Friendly name shown in the "waiting to sign in" status message.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Redirect wait timeout in seconds. 0 uses the host default.</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>Extra provider-specific authorization parameters (e.g. <c>prompt</c>, <c>audience</c>).</summary>
    public IReadOnlyDictionary<string, string>? AdditionalAuthorizationParameters { get; init; }

    /// <summary>
    /// Run the interactive Authorization Code + PKCE flow and return the tokens.
    /// </summary>
    /// <exception cref="NotSupportedException">The host does not support authentication.</exception>
    /// <exception cref="OAuthException">The flow or token exchange failed.</exception>
    public async Task<OAuthToken> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        var (verifier, challenge) = Pkce.Generate();

        var parameters = BuildAuthorizationParameters(challenge);

        var request = new AuthorizationRequest
        {
            DisplayName = DisplayName,
            AuthorizationEndpoint = AuthorizationEndpoint,
            Parameters = parameters,
            RedirectKind = RedirectKind,
            TimeoutSeconds = TimeoutSeconds,
        };

        var result = await ExtensionHost.RequestAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccessful)
        {
            throw new OAuthException(string.IsNullOrEmpty(result.Error) ? "Authorization failed." : result.Error);
        }

        var response = result.ResponseParameters;
        if (response.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error))
        {
            response.TryGetValue("error_description", out var description);
            throw new OAuthException(string.IsNullOrEmpty(description) ? error : description, error);
        }

        if (!response.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            throw new OAuthException("The authorization response did not contain an authorization code.");
        }

        var body = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = result.RedirectUri,
            ["client_id"] = ClientId,
            ["code_verifier"] = verifier,
        };

        return await ExchangeAsync(body, cancellationToken).ConfigureAwait(false);
    }

    internal Dictionary<string, string> BuildAuthorizationParameters(string challenge)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };

        if (Scopes is { Count: > 0 })
        {
            parameters["scope"] = string.Join(' ', Scopes);
        }

        if (AdditionalAuthorizationParameters is not null)
        {
            foreach (var pair in AdditionalAuthorizationParameters)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Exchange a refresh token for a new token set.
    /// </summary>
    /// <exception cref="OAuthException">The refresh failed.</exception>
    public async Task<OAuthToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var body = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
        };

        if (Scopes is { Count: > 0 })
        {
            body["scope"] = string.Join(' ', Scopes);
        }

        return await ExchangeAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthToken> ExchangeAsync(IDictionary<string, string> body, CancellationToken cancellationToken)
    {
        string json;
        bool success;
        try
        {
            using var content = new FormUrlEncodedContent(body);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = content };

            // Some providers (e.g. GitHub) default to form-encoded responses
            // unless the client explicitly asks for JSON.
            httpRequest.Headers.Accept.ParseAdd("application/json");

            using var httpResponse = await SharedHttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            success = httpResponse.IsSuccessStatusCode;
            json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                throw new OAuthException(
                    TryParseError(json) ?? $"The token endpoint returned HTTP {(int)httpResponse.StatusCode}.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new OAuthException("The token request could not be sent.", ex);
        }

        return ParseTokenResponse(json);
    }

    private static OAuthToken ParseTokenResponse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new OAuthException("The token response was not valid JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new OAuthException("The token response was not a JSON object.");
            }

            if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.String)
            {
                var errorCode = errorEl.GetString();
                var description = root.TryGetProperty("error_description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                    ? descEl.GetString()
                    : null;
                throw new OAuthException(string.IsNullOrEmpty(description) ? (errorCode ?? "Token request failed.") : description, errorCode);
            }

            var accessToken = GetString(root, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new OAuthException("The token response did not contain an access_token.");
            }

            DateTimeOffset? expiresAt = null;
            if (root.TryGetProperty("expires_in", out var expiresEl))
            {
                long? seconds = expiresEl.ValueKind switch
                {
                    JsonValueKind.Number when expiresEl.TryGetInt64(out var n) => n,
                    JsonValueKind.String when long.TryParse(expiresEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
                    _ => null,
                };
                if (seconds is long secs)
                {
                    expiresAt = DateTimeOffset.UtcNow.AddSeconds(secs);
                }
            }

            return new OAuthToken
            {
                AccessToken = accessToken,
                RefreshToken = GetString(root, "refresh_token"),
                TokenType = GetString(root, "token_type"),
                Scope = GetString(root, "scope"),
                IdToken = GetString(root, "id_token"),
                ExpiresAt = expiresAt,
            };
        }
    }

    private static string? TryParseError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var errorEl) &&
                errorEl.ValueKind == JsonValueKind.String)
            {
                var description = root.TryGetProperty("error_description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                    ? descEl.GetString()
                    : null;
                return string.IsNullOrEmpty(description) ? errorEl.GetString() : description;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
