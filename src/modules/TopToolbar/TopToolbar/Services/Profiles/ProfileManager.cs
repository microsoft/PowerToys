// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using TopToolbar.Logging;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// High-level orchestrator combining registry, store and provider catalog.
/// Handles profile CRUD, switching, and override mutations.
/// </summary>
public sealed class ProfileManager : IProfileManager
{
    private readonly IProfileRegistry _registry;
    private readonly IProfileStore _store;
    private readonly IProviderDefinitionCatalog _catalog; // reserved for future validation
    private readonly ProfileFileService _profileFileService;

    private ProfilesRegistry _currentRegistry;
    private ProfileOverridesFile _activeOverrides;

    public event EventHandler ActiveProfileChanged;

    public event EventHandler ProfilesChanged;

    public event EventHandler OverridesChanged;

    public ProfileManager(IProfileRegistry registry, IProfileStore store, IProviderDefinitionCatalog catalog, ProfileFileService profileFileService = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _profileFileService = profileFileService ?? new ProfileFileService(null, registry);

        _currentRegistry = _registry.Load();
        _activeOverrides = _store.Load(_currentRegistry.ActiveProfileId);
    }

    public string ActiveProfileId => _currentRegistry.ActiveProfileId;

    public IReadOnlyList<ProfileMeta> GetProfiles() => _currentRegistry.Profiles;

    public ProfileOverridesFile GetActiveOverrides() => _activeOverrides;

    public void SwitchProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, _currentRegistry.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_currentRegistry.Profiles.Any(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _registry.SetActive(profileId);
        _currentRegistry = _registry.Load();
        _activeOverrides = _store.Load(profileId);
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CreateProfile(string newProfileId, string name, bool cloneCurrent)
    {
        newProfileId = (newProfileId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newProfileId))
        {
            throw new ArgumentException("Profile id required", nameof(newProfileId));
        }

        if (_currentRegistry.Profiles.Any(p => string.Equals(p.Id, newProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Profile id already exists.");
        }

        var meta = new ProfileMeta { Id = newProfileId, Name = string.IsNullOrWhiteSpace(name) ? newProfileId : name.Trim() };
        _currentRegistry.Profiles.Add(meta);
        _registry.Save(_currentRegistry);

        // Create complete profile configuration file
        var newProfile = _profileFileService.CreateEmptyProfile(newProfileId, meta.Name);
        _profileFileService.SaveProfile(newProfile);

        ProfileOverridesFile newFile;
        if (cloneCurrent)
        {
            var clone = new ProfileOverridesFile
            {
                ProfileId = newProfileId,
                Overrides = new ProfileOverrides
                {
                    Groups = new Dictionary<string, bool>(_activeOverrides.Overrides.Groups, StringComparer.OrdinalIgnoreCase),
                    Actions = new Dictionary<string, bool>(_activeOverrides.Overrides.Actions, StringComparer.OrdinalIgnoreCase),
                },
            };
            newFile = clone;
        }
        else
        {
            newFile = new ProfileOverridesFile { ProfileId = newProfileId };
        }

        _store.Save(newFile);
        _currentRegistry = _registry.Load();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RenameProfile(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase))
        {
            return; // do not rename default
        }

        var meta = _currentRegistry.Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (meta == null)
        {
            return;
        }

        if (string.Equals(meta.Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        meta.Name = newName.Trim();
        _registry.Save(_currentRegistry);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var meta = _currentRegistry.Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (meta == null)
        {
            return;
        }

        // Delete the complete profile configuration file
        _profileFileService.DeleteProfile(profileId);

        _currentRegistry.Profiles.Remove(meta);
        if (string.Equals(_currentRegistry.ActiveProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            _currentRegistry.ActiveProfileId = "default";
        }

        _registry.Save(_currentRegistry);
        _currentRegistry = _registry.Load();
        _activeOverrides = _store.Load(_currentRegistry.ActiveProfileId);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateGroup(string groupId, bool? enabled)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        if (enabled.HasValue)
        {
            _activeOverrides.Overrides.Groups[groupId] = enabled.Value;
        }
        else
        {
            _activeOverrides.Overrides.Groups.Remove(groupId);
        }

        _store.Save(_activeOverrides);
        OverridesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateAction(string actionId, bool? enabled)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        if (enabled.HasValue)
        {
            _activeOverrides.Overrides.Actions[actionId] = enabled.Value;
        }
        else
        {
            _activeOverrides.Overrides.Actions.Remove(actionId);
        }

        _store.Save(_activeOverrides);
        OverridesChanged?.Invoke(this, EventArgs.Empty);
    }
}
