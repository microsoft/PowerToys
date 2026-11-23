// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Represents a single key-to-characters mapping row within a language entry in the
    /// Quick Accent reference guide.
    /// </summary>
    public sealed record KeyMappingModel(string KeyLabel, IReadOnlyList<CharacterModel> Characters);
}
