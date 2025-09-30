// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Abstraction for loading provider definition files (metadata + defaults) from disk (and later, dynamic sources).
/// </summary>
public interface IProviderDefinitionCatalog
{
    /// <summary>
    /// Loads (or reloads) all provider definition files from the configured root.
    /// </summary>
    /// <returns>Dictionary keyed by providerId.</returns>
    IReadOnlyDictionary<string, ProviderDefinitionFile> LoadAll();
}
