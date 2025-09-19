// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Models;
using TopToolbar.Providers.Configuration;

namespace TopToolbar.Providers;

public sealed class ConfiguredActionProvider : IActionProvider, IToolbarGroupProvider
{
    private readonly ProviderConfig _config;
    private readonly Dictionary<string, ProviderActionConfig> _actionMap;
    private readonly List<(ProviderActionConfig Config, double Order, int Index)> _orderedActions;

    public ConfiguredActionProvider(ProviderConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        (_actionMap, _orderedActions) = BuildActionIndex(config.Actions);
    }

    public string Id => _config.Id;

    public Task<ProviderInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        var displayName = string.IsNullOrWhiteSpace(_config.GroupName) ? _config.Id : _config.GroupName;
        return Task.FromResult(new ProviderInfo(displayName, string.Empty));
    }

    public async IAsyncEnumerable<ActionDescriptor> DiscoverAsync(ActionContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        foreach (var entry in _orderedActions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateDescriptor(entry.Config);
        }
    }

    public Task<ButtonGroup> CreateGroupAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var group = new ButtonGroup
        {
            Id = _config.Id,
            Name = string.IsNullOrWhiteSpace(_config.GroupName) ? _config.Id : _config.GroupName,
            Description = _config.Description,
            Layout = BuildLayout(_config.Layout),
        };

        foreach (var entry in _orderedActions)
        {
            var button = BuildButton(entry.Config);
            group.Buttons.Add(button);
        }

        return Task.FromResult(group);
    }

    public Task<ActionResult> InvokeAsync(
        string actionId,
        JsonElement? args,
        ActionContext context,
        IProgress<ActionProgress> progress,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ActionResult
        {
            Ok = false,
            Message = "Configured provider actions execute directly.",
        });
    }

    private static (Dictionary<string, ProviderActionConfig> Map, List<(ProviderActionConfig Config, double Order, int Index)> Ordered) BuildActionIndex(IEnumerable<ProviderActionConfig> actions)
    {
        var map = new Dictionary<string, ProviderActionConfig>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<(ProviderActionConfig Config, double Order, int Index)>();

        if (actions == null)
        {
            return (map, ordered);
        }

        var index = 0;
        foreach (var action in actions)
        {
            if (action == null)
            {
                index++;
                continue;
            }

            if (action.IsEnabled.HasValue && !action.IsEnabled.Value)
            {
                index++;
                continue;
            }

            var key = string.IsNullOrWhiteSpace(action.Id) ? Guid.NewGuid().ToString() : action.Id;

            if (map.TryGetValue(key, out _))
            {
                map[key] = action;

                for (var i = 0; i < ordered.Count; i++)
                {
                    if (!string.Equals(ordered[i].Config?.Id, key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var current = ordered[i];
                    ordered[i] = (Config: action, Order: action.SortOrder ?? double.MaxValue, Index: current.Index);
                    break;
                }
            }
            else
            {
                map[key] = action;
                ordered.Add((Config: action, Order: action.SortOrder ?? double.MaxValue, Index: index));
            }

            index++;
        }

        ordered.Sort((left, right) =>
        {
            var orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return left.Index.CompareTo(right.Index);
        });

        return (map, ordered);
    }

    private static ToolbarGroupLayout BuildLayout(ProviderLayoutConfig layout)
    {
        var result = new ToolbarGroupLayout();
        if (layout == null)
        {
            return result;
        }

        if (layout.Style.HasValue)
        {
            result.Style = layout.Style.Value;
        }

        if (layout.Overflow.HasValue)
        {
            result.Overflow = layout.Overflow.Value;
        }

        if (layout.MaxInline.HasValue)
        {
            result.MaxInline = layout.MaxInline;
        }

        if (layout.ShowLabels.HasValue)
        {
            result.ShowLabels = layout.ShowLabels.Value;
        }

        return result;
    }

    private static ToolbarButton BuildButton(ProviderActionConfig action)
    {
        var button = new ToolbarButton
        {
            Id = string.IsNullOrWhiteSpace(action.Id) ? Guid.NewGuid().ToString() : action.Id,
            Name = action.Name,
            Description = action.Description,
            IconGlyph = action.IconGlyph,
            IconPath = action.IconPath,
            Action = action.Action?.Clone() ?? new ToolbarAction(),
        };

        if (action.IconType.HasValue)
        {
            button.IconType = action.IconType.Value;
        }
        else if (!string.IsNullOrWhiteSpace(action.IconPath))
        {
            button.IconType = ToolbarIconType.Image;
        }
        else
        {
            button.IconType = ToolbarIconType.Glyph;
        }

        if (action.SortOrder.HasValue)
        {
            button.SortOrder = action.SortOrder;
        }

        return button;
    }

    private ActionDescriptor CreateDescriptor(ProviderActionConfig action)
    {
        var descriptorId = string.IsNullOrWhiteSpace(action.Id) ? Guid.NewGuid().ToString() : action.Id;
        var descriptor = new ActionDescriptor
        {
            Id = descriptorId,
            ProviderId = Id,
            Title = string.IsNullOrWhiteSpace(action.Name) ? descriptorId : action.Name,
            Subtitle = action.Description,
            Kind = ActionKind.Command,
            Icon = BuildActionIcon(action),
            Order = action.SortOrder,
            CanExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(action.Name))
        {
            descriptor.Keywords.Add(action.Name);
        }

        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            descriptor.Keywords.Add(action.Description);
        }

        return descriptor;
    }

    private static ActionIcon BuildActionIcon(ProviderActionConfig action)
    {
        if (!string.IsNullOrWhiteSpace(action.IconGlyph))
        {
            return new ActionIcon { Type = ActionIconType.Glyph, Value = action.IconGlyph };
        }

        if (!string.IsNullOrWhiteSpace(action.IconPath))
        {
            return new ActionIcon { Type = ActionIconType.Bitmap, Value = action.IconPath };
        }

        return new ActionIcon { Type = ActionIconType.Glyph, Value = string.Empty };
    }
}
