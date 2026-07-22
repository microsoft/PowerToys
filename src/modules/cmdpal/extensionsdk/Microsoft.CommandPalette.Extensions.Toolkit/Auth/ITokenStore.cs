// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A place to persist <see cref="OAuthToken"/>s between sessions. Implementations
/// run inside the extension's process; Command Palette never sees stored tokens.
/// </summary>
public interface ITokenStore
{
    /// <summary>Retrieve a previously saved token, or null if none exists.</summary>
    OAuthToken? Retrieve(string key);

    /// <summary>Save (or overwrite) a token under <paramref name="key"/>.</summary>
    void Save(string key, OAuthToken token);

    /// <summary>Remove any token saved under <paramref name="key"/>.</summary>
    void Remove(string key);
}
