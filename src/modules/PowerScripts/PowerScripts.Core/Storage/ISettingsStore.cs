// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Storage;

/// <summary>
/// A small key/value blob store for the module's security-bearing settings — the trust store and the
/// module <c>config.json</c>. It is deliberately shaped after the per-user protected-settings broker
/// (the <c>Ping</c> / <c>GetBlob</c> / <c>PutBlob</c> named-pipe service): each logical asset is an
/// opaque text blob addressed by a key within the PowerScripts namespace.
///
/// Today the only implementation is <see cref="FileSettingsStore"/>, which keeps the exact on-disk
/// layout the module has always used (so nothing that reads those files directly breaks). When the
/// protected settings service lands, a broker-backed implementation can be dropped in behind
/// <see cref="SettingsStore.Current"/> without touching any caller: reads/writes simply travel over
/// the pipe to the admin-owned, tamper-resistant store instead of a user-writable file.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Reads the text blob stored under <paramref name="key"/>, or <c>null</c> when it does not exist
    /// (or cannot be read). Analogous to the broker's <c>GetBlob</c> returning <c>NotFound</c>.
    /// </summary>
    string? ReadBlob(string key);

    /// <summary>
    /// Writes (creating or overwriting) the text blob stored under <paramref name="key"/>. Analogous
    /// to the broker's <c>PutBlob</c>. In the future protected-store implementation this is the only
    /// path through which the security-bearing settings are mutated.
    /// </summary>
    void WriteBlob(string key, string contents);
}
