// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

public interface IEffectiveModelBuilder
{
    IReadOnlyList<EffectiveProviderModel> Build(ProfileOverridesFile profile, IReadOnlyDictionary<string, ProviderDefinitionFile> providers);
}
