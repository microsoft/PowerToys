// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Trim/AOT-safe (reflection-free) serialization of <see cref="OAuthToken"/> to a
/// compact JSON blob, used by token stores. This is deliberately not the OAuth
/// token-endpoint wire format; it is our own storage format.
/// </summary>
internal static class OAuthTokenSerialization
{
    public static string Serialize(OAuthToken token)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("access_token", token.AccessToken);
            if (token.RefreshToken is not null)
            {
                writer.WriteString("refresh_token", token.RefreshToken);
            }

            if (token.TokenType is not null)
            {
                writer.WriteString("token_type", token.TokenType);
            }

            if (token.Scope is not null)
            {
                writer.WriteString("scope", token.Scope);
            }

            if (token.IdToken is not null)
            {
                writer.WriteString("id_token", token.IdToken);
            }

            if (token.ExpiresAt is DateTimeOffset expiresAt)
            {
                writer.WriteString("expires_at", expiresAt.ToString("o", CultureInfo.InvariantCulture));
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static OAuthToken? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            DateTimeOffset? expiresAt = null;
            if (root.TryGetProperty("expires_at", out var expiresEl) &&
                expiresEl.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(expiresEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                expiresAt = parsed;
            }

            return new OAuthToken
            {
                AccessToken = GetString(root, "access_token") ?? string.Empty,
                RefreshToken = GetString(root, "refresh_token"),
                TokenType = GetString(root, "token_type"),
                Scope = GetString(root, "scope"),
                IdToken = GetString(root, "id_token"),
                ExpiresAt = expiresAt,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
