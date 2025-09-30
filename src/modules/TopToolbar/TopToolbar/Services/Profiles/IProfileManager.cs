// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

public interface IProfileManager
{
    event EventHandler ActiveProfileChanged;

    event EventHandler ProfilesChanged;

    event EventHandler OverridesChanged;

    string ActiveProfileId { get; }

    IReadOnlyList<ProfileMeta> GetProfiles();

    ProfileOverridesFile GetActiveOverrides();

    void SwitchProfile(string profileId);

    void CreateProfile(string newProfileId, string name, bool cloneCurrent);

    void RenameProfile(string profileId, string newName);

    void DeleteProfile(string profileId);

    void UpdateGroup(string groupId, bool? enabled); // null = remove override

    void UpdateAction(string actionId, bool? enabled); // null = remove override
}
