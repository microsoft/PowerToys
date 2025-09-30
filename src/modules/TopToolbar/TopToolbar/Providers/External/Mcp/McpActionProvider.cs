// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Providers.Configuration;

namespace TopToolbar.Providers.External.Mcp;

public sealed class McpActionProvider : IActionProvider, IToolbarGroupProvider, IDisposable
{
    private const string DefaultGlyph = "\uECAA";
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*(?<key>[^}]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private readonly ProviderConfig _config;
    private readonly ExternalProviderConfig _external;
    private readonly ExternalActionProviderHost _host;
    private readonly McpClient _client;
    private readonly Dictionary<string, ProviderActionConfig> _actionsByProviderActionId;
    private readonly Dictionary<string, ProviderActionConfig> _actionsById;
    private readonly Dictionary<string, DynamicActionCacheEntry> _dynamicCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly TimeSpan _toolCacheDuration = TimeSpan.FromSeconds(15);

    private IReadOnlyList<McpToolInfo> _cachedTools = Array.Empty<McpToolInfo>();
    private DateTime _toolsTimestamp = DateTime.MinValue;
    private bool _disposed;

    public McpActionProvider(ProviderConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _external = _config.External ?? throw new InvalidOperationException("Provider configuration is missing external settings.");
        if (_external.Type != ExternalProviderType.Mcp)
        {
            throw new InvalidOperationException("Provider configuration is not an MCP provider.");
        }

        var environment = _external.Environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        TimeSpan? shutdownTimeout = _external.StartupTimeoutSeconds.HasValue
            ? TimeSpan.FromSeconds(Math.Max(1, _external.StartupTimeoutSeconds.Value))
            : null;

        _host = new ExternalActionProviderHost(
            _config.Id,
            _external.ExecutablePath,
            _external.Arguments,
            _external.WorkingDirectory,
            environment,
            shutdownTimeout);

        _client = new McpClient(_host);
        _actionsByProviderActionId = BuildActionIndex(_config.Actions, action => action?.Action?.ProviderActionId);
        _actionsById = BuildActionIndex(_config.Actions, action => action?.Id);
    }

    public string Id => _config.Id;

    public async Task<ProviderInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var displayName = string.IsNullOrWhiteSpace(_config.GroupName) ? _config.Id : _config.GroupName;
        return new ProviderInfo(displayName, "mcp");
    }

    public async IAsyncEnumerable<ActionDescriptor> DiscoverAsync(ActionContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolInfo> tools;
        try
        {
            tools = await GetToolsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"McpActionProvider[{Id}]: discovery failed - {ex.Message}.");
            yield break;
        }

        IReadOnlyList<ResolvedAction> resolved;
        try
        {
            resolved = await ResolveActionsAsync(tools, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"McpActionProvider[{Id}]: failed to resolve actions for discovery - {ex.Message}.");
            yield break;
        }

        foreach (var action in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateDescriptor(action.Config, action.Tool);
        }
    }

    public async Task<ButtonGroup> CreateGroupAsync(ActionContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolInfo> tools;
        try
        {
            tools = await GetToolsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"McpActionProvider[{Id}]: failed to build button group - {ex.Message}.");
            return CreateEmptyGroup();
        }

        IReadOnlyList<ResolvedAction> resolved;
        try
        {
            resolved = await ResolveActionsAsync(tools, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"McpActionProvider[{Id}]: failed to resolve actions for group - {ex.Message}.");
            return CreateEmptyGroup();
        }

        var group = new ButtonGroup
        {
            Id = Id,
            Name = string.IsNullOrWhiteSpace(_config.GroupName) ? Id : _config.GroupName,
            Description = _config.Description,
            Layout = BuildLayout(_config.Layout),
        };

        foreach (var action in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var button = BuildButton(action.Config);
            group.Buttons.Add(button);
        }

        return group;
    }

    public async Task<ActionResult> InvokeAsync(
        string actionId,
        JsonElement? args,
        ActionContext context,
        IProgress<ActionProgress> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return new ActionResult
            {
                Ok = false,
                Message = "Action id is required.",
            };
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            var result = await _client.CallToolAsync(actionId, args, cancellationToken).ConfigureAwait(false);
            return BuildActionResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (McpException ex)
        {
            AppLogger.LogWarning($"McpActionProvider[{Id}]: MCP error - {ex.Message} (code {ex.ErrorCode}).");
            return new ActionResult
            {
                Ok = false,
                Message = ex.Message,
                Output = new ActionOutput
                {
                    Type = "application/json",
                    Data = ex.ErrorDetails,
                },
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"McpActionProvider[{Id}]: invoke failed - {ex.Message}.");
            return new ActionResult
            {
                Ok = false,
                Message = ex.Message,
            };
        }
    }

    private async Task<IReadOnlyList<ResolvedAction>> ResolveActionsAsync(IReadOnlyList<McpToolInfo> tools, CancellationToken cancellationToken)
    {
        var results = new List<ResolvedAction>();
        var sequence = 0;

        foreach (var tool in tools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var variants = await ExpandVariantsAsync(tool, cancellationToken).ConfigureAwait(false);
            foreach (var variant in variants)
            {
                results.Add(new ResolvedAction(variant.Config, variant.Tool, variant.Order, sequence++));
            }
        }

        results.Sort(static (left, right) =>
        {
            var orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return left.Sequence.CompareTo(right.Sequence);
        });

        return results;
    }

    private async Task<IReadOnlyList<ResolvedVariant>> ExpandVariantsAsync(McpToolInfo tool, CancellationToken cancellationToken)
    {
        var baseConfig = GetActionConfig(tool.Name);
        if (baseConfig == null)
        {
            var fallbackConfig = new ProviderActionConfig
            {
                Id = tool.Name,
                Name = string.IsNullOrWhiteSpace(tool.Name) ? "MCP Tool" : tool.Name,
                Description = tool.Description,
                IconGlyph = DefaultGlyph,
                SortOrder = double.MaxValue,
                Action = new ToolbarAction
                {
                    Type = ToolbarActionType.Provider,
                    ProviderId = Id,
                    ProviderActionId = tool.Name,
                },
                Input = new ProviderActionInputConfig(),
            };

            return new[] { new ResolvedVariant(fallbackConfig, tool, fallbackConfig.SortOrder ?? double.MaxValue) };
        }

        var input = baseConfig.Input;
        var hasInput = HasInputConfiguration(input);
        var baseOrder = baseConfig.SortOrder ?? double.MaxValue;
        var variants = new List<ResolvedVariant>();

        if (hasInput)
        {
            if (input.Enum != null && input.Enum.Count > 0)
            {
                variants.AddRange(ExpandEnumVariants(tool, baseConfig, input, baseOrder));
            }

            if (input.Dynamic != null)
            {
                var dynamicVariants = await ExpandDynamicVariantsAsync(tool, baseConfig, input, input.Dynamic, baseOrder, cancellationToken).ConfigureAwait(false);
                variants.AddRange(dynamicVariants);
            }

            var hideBase = input.HideBaseAction ?? ((input.Enum?.Count ?? 0) > 0 || input.Dynamic != null);
            if (!hideBase)
            {
                var clone = CloneActionConfig(baseConfig);
                clone.SortOrder = clone.SortOrder ?? baseOrder;
                variants.Add(new ResolvedVariant(clone, tool, clone.SortOrder ?? baseOrder));
            }
        }
        else
        {
            variants.Add(new ResolvedVariant(baseConfig, tool, baseOrder));
        }

        if (variants.Count == 0)
        {
            var clone = CloneActionConfig(baseConfig);
            clone.SortOrder = clone.SortOrder ?? baseOrder;
            variants.Add(new ResolvedVariant(clone, tool, clone.SortOrder ?? baseOrder));
        }

        return variants;
    }

    private IEnumerable<ResolvedVariant> ExpandEnumVariants(
        McpToolInfo tool,
        ProviderActionConfig baseAction,
        ProviderActionInputConfig input,
        double baseOrder)
    {
        if (input.Enum == null || input.Enum.Count == 0)
        {
            return Array.Empty<ResolvedVariant>();
        }

        var results = new List<ResolvedVariant>();

        for (var index = 0; index < input.Enum.Count; index++)
        {
            var option = input.Enum[index];
            if (option == null)
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = option.Value ?? string.Empty,
                ["label"] = option.Label ?? string.Empty,
                ["description"] = option.Description ?? string.Empty,
            };

            var clone = CloneActionConfig(baseAction);
            clone.Id = BuildVariantId(baseAction.Id, option.Value, index);
            clone.Name = string.IsNullOrWhiteSpace(option.Label)
                ? (string.IsNullOrWhiteSpace(baseAction.Name) ? option.Value : baseAction.Name)
                : option.Label;
            clone.Description = string.IsNullOrWhiteSpace(option.Description) ? baseAction.Description : option.Description;

            var glyph = string.IsNullOrWhiteSpace(option.IconGlyph) ? input.IconGlyph : option.IconGlyph;
            var iconPath = string.IsNullOrWhiteSpace(option.IconPath) ? input.IconPath : option.IconPath;
            clone.IconGlyph = string.IsNullOrWhiteSpace(glyph) ? baseAction.IconGlyph : glyph;
            clone.IconPath = string.IsNullOrWhiteSpace(iconPath) ? baseAction.IconPath : iconPath;
            clone.IconType = option.IconType ?? input.IconType ?? baseAction.IconType;

            UpdateToolbarAction(clone, option.Args ?? input.ArgsTemplate, values, option.Value);

            var order = option.SortOrder ?? baseAction.SortOrder ?? (baseOrder + (index * 0.001));
            clone.SortOrder = order;
            results.Add(new ResolvedVariant(clone, tool, order));
        }

        return results;
    }

    private async Task<IReadOnlyList<ResolvedVariant>> ExpandDynamicVariantsAsync(
        McpToolInfo tool,
        ProviderActionConfig baseAction,
        ProviderActionInputConfig input,
        ProviderActionDynamicInputConfig dynamic,
        double baseOrder,
        CancellationToken cancellationToken)
    {
        if (dynamic == null || string.IsNullOrWhiteSpace(dynamic.SourceTool))
        {
            return Array.Empty<ResolvedVariant>();
        }

        var items = await GetDynamicItemsAsync(dynamic, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            return Array.Empty<ResolvedVariant>();
        }

        var results = new List<ResolvedVariant>();
        var maxItems = dynamic.MaxItems ?? int.MaxValue;

        for (var index = 0; index < items.Count && index < maxItems; index++)
        {
            var item = items[index];
            var fields = new Dictionary<string, string>(item.Fields, StringComparer.OrdinalIgnoreCase);

            var value = string.Empty;
            if (!string.IsNullOrWhiteSpace(dynamic.ValueField))
            {
                fields.TryGetValue(dynamic.ValueField, out value);
            }

            var label = ResolveLabel(fields, input, dynamic, baseAction.Name, value);
            fields["label"] = label;
            fields["value"] = value ?? string.Empty;
            var description = ResolveDescription(fields, input, dynamic, baseAction.Description);

            var clone = CloneActionConfig(baseAction);
            clone.Id = BuildVariantId(baseAction.Id, value, index);
            clone.Name = label;
            clone.Description = description;

            ApplyDynamicIcon(clone, input, dynamic, fields, baseAction);

            UpdateToolbarAction(clone, dynamic.ArgsTemplate ?? input.ArgsTemplate, fields, value);

            double order;
            if (!string.IsNullOrWhiteSpace(dynamic.SortField) &&
                fields.TryGetValue(dynamic.SortField, out var sortValue) &&
                double.TryParse(sortValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSort))
            {
                order = parsedSort;
            }
            else
            {
                order = baseAction.SortOrder ?? (baseOrder + (index * 0.001));
            }

            clone.SortOrder = order;
            results.Add(new ResolvedVariant(clone, tool, order));
        }

        return results;
    }

    private async Task<IReadOnlyList<DynamicItem>> GetDynamicItemsAsync(ProviderActionDynamicInputConfig dynamic, CancellationToken cancellationToken)
    {
        var cacheKey = $"{dynamic.SourceTool}::{dynamic.ItemsPath}";
        var cacheDuration = dynamic.CacheSeconds.HasValue
            ? TimeSpan.FromSeconds(Math.Max(1, dynamic.CacheSeconds.Value))
            : _toolCacheDuration;

        if (_dynamicCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.TimestampUtc < cached.Duration)
        {
            return cached.Items;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var response = await _client.CallToolAsync(dynamic.SourceTool, null, cancellationToken).ConfigureAwait(false);

        if (!TryResolveJsonPath(response, dynamic.ItemsPath, out var collection) || collection.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DynamicItem>();
        }

        var items = new List<DynamicItem>();
        foreach (var element in collection.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                fields[property.Name] = ConvertJsonElementToString(property.Value);
            }

            items.Add(new DynamicItem(fields));
        }

        var entry = new DynamicActionCacheEntry(DateTime.UtcNow, cacheDuration, items);
        _dynamicCache[cacheKey] = entry;
        return entry.Items;
    }

    private static string ResolveLabel(
        IReadOnlyDictionary<string, string> fields,
        ProviderActionInputConfig input,
        ProviderActionDynamicInputConfig dynamic,
        string fallbackName,
        string fallbackValue)
    {
        if (!string.IsNullOrWhiteSpace(dynamic.LabelTemplate))
        {
            var template = ApplyTemplate(dynamic.LabelTemplate, fields);
            if (!string.IsNullOrWhiteSpace(template))
            {
                return template;
            }
        }

        if (!string.IsNullOrWhiteSpace(dynamic.LabelField) &&
            fields.TryGetValue(dynamic.LabelField, out var labelFromField) &&
            !string.IsNullOrWhiteSpace(labelFromField))
        {
            return labelFromField;
        }

        if (!string.IsNullOrWhiteSpace(input.LabelTemplate))
        {
            var template = ApplyTemplate(input.LabelTemplate, fields);
            if (!string.IsNullOrWhiteSpace(template))
            {
                return template;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            return fallbackName;
        }

        return fallbackValue ?? string.Empty;
    }

    private static string ResolveDescription(
        IReadOnlyDictionary<string, string> fields,
        ProviderActionInputConfig input,
        ProviderActionDynamicInputConfig dynamic,
        string fallbackDescription)
    {
        if (!string.IsNullOrWhiteSpace(dynamic.DescriptionTemplate))
        {
            var template = ApplyTemplate(dynamic.DescriptionTemplate, fields);
            if (!string.IsNullOrWhiteSpace(template))
            {
                return template;
            }
        }

        if (!string.IsNullOrWhiteSpace(dynamic.DescriptionField) &&
            fields.TryGetValue(dynamic.DescriptionField, out var descriptionFromField) &&
            !string.IsNullOrWhiteSpace(descriptionFromField))
        {
            return descriptionFromField;
        }

        if (!string.IsNullOrWhiteSpace(input.DescriptionTemplate))
        {
            var template = ApplyTemplate(input.DescriptionTemplate, fields);
            if (!string.IsNullOrWhiteSpace(template))
            {
                return template;
            }
        }

        return fallbackDescription ?? string.Empty;
    }

    private static void ApplyDynamicIcon(
        ProviderActionConfig target,
        ProviderActionInputConfig input,
        ProviderActionDynamicInputConfig dynamic,
        IReadOnlyDictionary<string, string> fields,
        ProviderActionConfig baseAction)
    {
        string glyph = null;
        if (!string.IsNullOrWhiteSpace(dynamic.IconGlyphField) && fields.TryGetValue(dynamic.IconGlyphField, out var glyphFromField))
        {
            glyph = glyphFromField;
        }
        else if (!string.IsNullOrWhiteSpace(input.IconGlyph))
        {
            glyph = input.IconGlyph;
        }
        else if (!string.IsNullOrWhiteSpace(baseAction.IconGlyph))
        {
            glyph = baseAction.IconGlyph;
        }

        string iconPath = null;
        if (!string.IsNullOrWhiteSpace(dynamic.IconPathField) && fields.TryGetValue(dynamic.IconPathField, out var pathFromField))
        {
            iconPath = pathFromField;
        }
        else if (!string.IsNullOrWhiteSpace(input.IconPath))
        {
            iconPath = input.IconPath;
        }
        else if (!string.IsNullOrWhiteSpace(baseAction.IconPath))
        {
            iconPath = baseAction.IconPath;
        }

        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            target.IconPath = iconPath;
            target.IconGlyph = string.Empty;
            target.IconType = dynamic.IconType ?? input.IconType ?? ToolbarIconType.Image;
        }
        else if (!string.IsNullOrWhiteSpace(glyph))
        {
            target.IconGlyph = glyph;
            target.IconPath = string.Empty;
            target.IconType = ToolbarIconType.Glyph;
        }
        else
        {
            target.IconGlyph = baseAction.IconGlyph;
            target.IconPath = baseAction.IconPath;
            target.IconType = baseAction.IconType;
        }
    }

    private void UpdateToolbarAction(ProviderActionConfig config, JsonNode template, IReadOnlyDictionary<string, string> values, string fallbackValue)
    {
        var action = config.Action ?? new ToolbarAction();
        action.Type = ToolbarActionType.Provider;
        action.ProviderId = string.IsNullOrWhiteSpace(action.ProviderId) ? Id : action.ProviderId;
        action.ProviderActionId = string.IsNullOrWhiteSpace(action.ProviderActionId)
            ? (!string.IsNullOrWhiteSpace(config.Action?.ProviderActionId) ? config.Action.ProviderActionId : config.Id)
            : action.ProviderActionId;

        (string Key, string Value)? fallback = !string.IsNullOrWhiteSpace(fallbackValue) ? ("value", fallbackValue) : null;
        var argsJson = RenderArgumentsJson(template, values, fallback);
        if (!string.IsNullOrEmpty(argsJson))
        {
            action.ProviderArgumentsJson = argsJson;
        }

        config.Action = action;
        config.Input = new ProviderActionInputConfig();
    }

    private async Task<IReadOnlyList<McpToolInfo>> GetToolsAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            if (_cachedTools.Count > 0 && now - _toolsTimestamp < _toolCacheDuration)
            {
                return _cachedTools;
            }

            var tools = await _client.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            _cachedTools = tools;
            _toolsTimestamp = now;
            return _cachedTools;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _client.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private ProviderActionConfig GetActionConfig(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        if (_actionsByProviderActionId.TryGetValue(toolName, out var byProviderAction))
        {
            return byProviderAction;
        }

        if (_actionsById.TryGetValue(toolName, out var byId))
        {
            return byId;
        }

        return null;
    }

    private static ButtonGroup CreateEmptyGroup()
    {
        return new ButtonGroup
        {
            Id = string.Empty,
            Name = string.Empty,
            Description = string.Empty,
            Layout = new ToolbarGroupLayout(),
        };
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
            IconType = action.IconType ?? (string.IsNullOrWhiteSpace(action.IconPath) ? ToolbarIconType.Glyph : ToolbarIconType.Image),
        };

        if (action.SortOrder.HasValue)
        {
            button.SortOrder = action.SortOrder.Value;
        }

        return button;
    }

    private ActionDescriptor CreateDescriptor(ProviderActionConfig action, McpToolInfo tool)
    {
        var descriptorId = string.IsNullOrWhiteSpace(action.Id) ? Guid.NewGuid().ToString() : action.Id;
        var descriptor = new ActionDescriptor
        {
            Id = descriptorId,
            ProviderId = Id,
            Title = string.IsNullOrWhiteSpace(action.Name) ? (string.IsNullOrWhiteSpace(tool?.Name) ? descriptorId : tool.Name) : action.Name,
            Subtitle = string.IsNullOrWhiteSpace(action.Description) ? tool?.Description ?? string.Empty : action.Description,
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

        return new ActionIcon { Type = ActionIconType.Glyph, Value = DefaultGlyph };
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

    private static ActionResult BuildActionResult(JsonElement result)
    {
        var output = new ActionOutput
        {
            Type = "application/json",
            Data = result.Clone(),
        };

        if (result.ValueKind == JsonValueKind.Object)
        {
            if (result.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Array && contentElement.GetArrayLength() > 0)
            {
                var item = contentElement[0];
                if (item.TryGetProperty("type", out var typeElement))
                {
                    output.Type = typeElement.GetString() ?? output.Type;
                }

                if (item.TryGetProperty("data", out var dataElement))
                {
                    output.Data = dataElement.Clone();
                }
            }
            else if (result.TryGetProperty("contents", out var contentsElement) && contentsElement.ValueKind == JsonValueKind.Array && contentsElement.GetArrayLength() > 0)
            {
                var item = contentsElement[0];
                if (item.TryGetProperty("mimeType", out var mimeElement))
                {
                    output.Type = mimeElement.GetString() ?? output.Type;
                }

                if (item.TryGetProperty("data", out var dataElement))
                {
                    output.Data = dataElement.Clone();
                }
            }
        }

        return new ActionResult
        {
            Ok = true,
            Message = string.Empty,
            Output = output,
        };
    }

    private static ProviderActionConfig CloneActionConfig(ProviderActionConfig source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ProviderActionConfig
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            IconGlyph = source.IconGlyph,
            IconPath = source.IconPath,
            IconType = source.IconType,
            SortOrder = source.SortOrder,
            IsEnabled = source.IsEnabled,
            Action = source.Action?.Clone() ?? new ToolbarAction(),
            Input = new ProviderActionInputConfig(),
        };
    }

    private static bool HasInputConfiguration(ProviderActionInputConfig input)
    {
        if (input == null)
        {
            return false;
        }

        if (input.Enum != null && input.Enum.Any(option => option != null))
        {
            return true;
        }

        return input.Dynamic != null;
    }

    private static string BuildVariantId(string baseId, string value, int index)
    {
        var segment = !string.IsNullOrWhiteSpace(value) ? SanitizeIdSegment(value) : $"variant{index}";
        if (string.IsNullOrWhiteSpace(baseId))
        {
            return segment;
        }

        return $"{baseId}::{segment}";
    }

    private static string SanitizeIdSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Guid.NewGuid().ToString("N");
        }

        var chars = value.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
        if (chars.Length == 0)
        {
            return Guid.NewGuid().ToString("N");
        }

        return new string(chars);
    }

    private static string RenderArgumentsJson(JsonNode template, IReadOnlyDictionary<string, string> values, (string Key, string Value)? fallback)
    {
        if (template == null)
        {
            if (fallback.HasValue && !string.IsNullOrEmpty(fallback.Value.Value))
            {
                var obj = new JsonObject
                {
                    [fallback.Value.Key] = fallback.Value.Value,
                };
                return obj.ToJsonString();
            }

            return string.Empty;
        }

        var json = template.ToJsonString();
        return ApplyTemplate(json, values);
    }

    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (values.TryGetValue(key, out var replacement) && replacement != null)
            {
                return replacement;
            }

            return string.Empty;
        });
    }

    private static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => element.ToString(),
        };
    }

    private static bool TryResolveJsonPath(JsonElement root, string path, out JsonElement result)
    {
        result = root;
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (!TryResolveSegment(ref result, segment))
            {
                result = default;
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveSegment(ref JsonElement current, string segment)
    {
        if (segment.Length == 0)
        {
            return true;
        }

        var position = 0;
        while (position < segment.Length && segment[position] != '[')
        {
            position++;
        }

        if (position > 0)
        {
            var propertyName = segment.Substring(0, position);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertyName, out current))
            {
                return false;
            }
        }

        while (position < segment.Length)
        {
            if (segment[position] != '[')
            {
                return false;
            }

            var end = segment.IndexOf(']', position + 1);
            if (end < 0)
            {
                return false;
            }

            var indexSpan = segment.Substring(position + 1, end - position - 1);
            if (!int.TryParse(indexSpan, out var arrayIndex) || arrayIndex < 0)
            {
                return false;
            }

            if (current.ValueKind != JsonValueKind.Array || arrayIndex >= current.GetArrayLength())
            {
                return false;
            }

            current = current[arrayIndex];
            position = end + 1;
        }

        return true;
    }

    private static Dictionary<string, ProviderActionConfig> BuildActionIndex(
        IEnumerable<ProviderActionConfig> actions,
        Func<ProviderActionConfig, string> selector)
    {
        var map = new Dictionary<string, ProviderActionConfig>(StringComparer.OrdinalIgnoreCase);
        if (actions == null)
        {
            return map;
        }

        foreach (var action in actions)
        {
            if (action == null)
            {
                continue;
            }

            var key = selector(action);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            map[key.Trim()] = action;
        }

        return map;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(McpActionProvider));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cacheLock.Dispose();
        _client.Dispose();
        _host.Dispose();
        _dynamicCache.Clear();
        GC.SuppressFinalize(this);
    }

    private sealed record ResolvedAction(ProviderActionConfig Config, McpToolInfo Tool, double Order, int Sequence);

    private sealed class ResolvedVariant
    {
        public ResolvedVariant(ProviderActionConfig config, McpToolInfo tool, double order)
        {
            Config = config;
            Tool = tool;
            Order = order;
        }

        public ProviderActionConfig Config { get; }

        public McpToolInfo Tool { get; }

        public double Order { get; }
    }

    private sealed class DynamicActionCacheEntry
    {
        public DynamicActionCacheEntry(DateTime timestampUtc, TimeSpan duration, IReadOnlyList<DynamicItem> items)
        {
            TimestampUtc = timestampUtc;
            Duration = duration;
            Items = items;
        }

        public DateTime TimestampUtc { get; }

        public TimeSpan Duration { get; }

        public IReadOnlyList<DynamicItem> Items { get; }
    }

    private sealed class DynamicItem
    {
        public DynamicItem(Dictionary<string, string> fields)
        {
            Fields = fields;
        }

        public Dictionary<string, string> Fields { get; }
    }
}
