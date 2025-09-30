// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using TopToolbar.Services.Profiles.Models;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Merges provider definitions with a profile's overrides to produce an effective enabled model.
/// </summary>
public sealed class EffectiveModelBuilder : IEffectiveModelBuilder
{
    public IReadOnlyList<EffectiveProviderModel> Build(ProfileOverridesFile profile, IReadOnlyDictionary<string, ProviderDefinitionFile> providers)
    {
        profile ??= new ProfileOverridesFile { ProfileId = "default", Overrides = new ProfileOverrides() };
        providers ??= new Dictionary<string, ProviderDefinitionFile>(StringComparer.OrdinalIgnoreCase);

        var results = new List<EffectiveProviderModel>();
        foreach (var def in providers.Values.OrderBy(p => p.ProviderId, StringComparer.OrdinalIgnoreCase))
        {
            if (def.Groups == null || def.Groups.Count == 0)
            {
                continue;
            }

            var effProvider = new EffectiveProviderModel
            {
                ProviderId = def.ProviderId,
                DisplayName = string.IsNullOrWhiteSpace(def.DisplayName) ? def.ProviderId : def.DisplayName,
            };

            foreach (var group in def.Groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.GroupId))
                {
                    continue;
                }

                var groupEnabled = ResolveGroupEnabled(group, profile.Overrides);
                if (!groupEnabled)
                {
                    // Even if group disabled we still record it (so UI can show disabled group if needed?)
                    // For now we skip disabled groups entirely to minimize render cost.
                    continue;
                }

                var effGroup = new EffectiveGroup
                {
                    GroupId = group.GroupId,
                    Enabled = groupEnabled,
                };

                foreach (var action in group.Actions ?? Enumerable.Empty<ProviderActionDef>())
                {
                    if (action == null || string.IsNullOrWhiteSpace(action.Name))
                    {
                        continue;
                    }

                    var actionId = BuildActionId(group.GroupId, action.Name);
                    var actionEnabled = ResolveActionEnabled(actionId, action, profile.Overrides);
                    if (!actionEnabled)
                    {
                        continue;
                    }

                    effGroup.Actions.Add(new EffectiveAction
                    {
                        ActionId = actionId,
                        Enabled = true,
                        DisplayName = string.IsNullOrWhiteSpace(action.DisplayName) ? action.Name : action.DisplayName,
                        IconGlyph = action.IconGlyph,
                        IconPath = action.IconPath,
                    });
                }

                if (effGroup.Actions.Count > 0)
                {
                    effProvider.Groups.Add(effGroup);
                }
            }

            if (effProvider.Groups.Count > 0)
            {
                results.Add(effProvider);
            }
        }

        return results;
    }

    private static bool ResolveGroupEnabled(ProviderGroupDef g, ProfileOverrides ov)
    {
        ov ??= new ProfileOverrides();
        if (ov.Groups.TryGetValue(g.GroupId, out var v))
        {
            return v;
        }

        return g.DefaultEnabled ?? true;
    }

    private static bool ResolveActionEnabled(string actionId, ProviderActionDef a, ProfileOverrides ov)
    {
        ov ??= new ProfileOverrides();
        if (ov.Actions.TryGetValue(actionId, out var v))
        {
            return v;
        }

        return a.DefaultEnabled ?? true;
    }

    private static string BuildActionId(string groupId, string actionName) => $"{groupId}->{actionName}";
}
