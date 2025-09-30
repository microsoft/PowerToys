// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

public interface IProfileRegistry
{
    ProfilesRegistry Load();

    void Save(ProfilesRegistry registry);

    void SetActive(string profileId);
}
