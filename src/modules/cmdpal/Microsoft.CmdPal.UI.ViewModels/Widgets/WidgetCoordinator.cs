// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public sealed partial class WidgetCoordinator : IDisposable
{
    private const string EmptyJson = "{}";
    private const int MaxPayloadLength = 256 * 1024;
    private static readonly TimeSpan ExtensionCallTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan UpdateDebounce = TimeSpan.FromMilliseconds(150);
    private readonly IWidgetCatalog _catalog;
    private readonly IWidgetPlatform _platform;
    private readonly ConcurrentDictionary<string, WidgetSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SelectorState> _selectors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _instanceGates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingUpdates = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    public WidgetCoordinator(IWidgetCatalog catalog, IWidgetPlatform platform)
    {
        _catalog = catalog;
        _platform = platform;
    }

    public Task CreateAsync(string instanceId, string? size, CancellationToken cancellationToken = default)
    {
        ShowLoading(instanceId);
        return ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                var session = await RestoreAsync(instanceId, null, ct).ConfigureAwait(false);
                if (session is null)
                {
                    await ShowSelectorCoreAsync(instanceId, null, ct).ConfigureAwait(false);
                    return;
                }

                session.Size = size;
                UpdateSession(session);
            },
            cancellationToken);
    }

    public Task DeleteAsync(string instanceId, string? customState, CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                _selectors.TryRemove(instanceId, out _);
                if (_sessions.TryRemove(instanceId, out var existing))
                {
                    await InvokeExtensionAsync(existing.Widget.Delete, ct).ConfigureAwait(false);
                    existing.Dispose();
                    return;
                }

                if (!WidgetBinding.TryParse(customState, out var binding))
                {
                    return;
                }

                await _catalog.RefreshAsync(ct).ConfigureAwait(false);
                var widget = await _catalog.CreateAsync(binding, instanceId, ct).ConfigureAwait(false);
                if (widget is not null)
                {
                    await InvokeExtensionAsync(widget.Delete, ct).ConfigureAwait(false);
                }
            },
            cancellationToken);

    public Task ActivateAsync(string instanceId, string? size, CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                var session = await RestoreAsync(instanceId, null, ct).ConfigureAwait(false);
                if (session is null)
                {
                    await ShowSelectorCoreAsync(instanceId, null, ct).ConfigureAwait(false);
                    return;
                }

                session.Size = size;
                if (!session.IsActive)
                {
                    await InvokeExtensionAsync(session.Widget.Activate, ct).ConfigureAwait(false);
                    session.IsActive = true;
                }

                UpdateSession(session);
            },
            cancellationToken);

    public Task DeactivateAsync(string instanceId, CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                if (_sessions.TryGetValue(instanceId, out var session) && session.IsActive)
                {
                    await InvokeExtensionAsync(session.Widget.Deactivate, ct).ConfigureAwait(false);
                    session.IsActive = false;
                }
            },
            cancellationToken);

    public Task ContextChangedAsync(string instanceId, string? size, CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                var session = await RestoreAsync(instanceId, null, ct).ConfigureAwait(false);
                if (session is null)
                {
                    return;
                }

                session.Size = size;
                UpdateSession(session);
            },
            cancellationToken);

    public Task ShowCustomizationAsync(string instanceId, CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
                _sessions.TryGetValue(instanceId, out var current);
                await ShowSelectorCoreAsync(instanceId, current?.Binding, ct).ConfigureAwait(false);
            },
            cancellationToken);

    public Task ActionAsync(
        string instanceId,
        string verb,
        string? data,
        string? customState,
        CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(
            instanceId,
            async ct =>
            {
            if (string.Equals(verb, "chooseWidget", StringComparison.Ordinal) ||
                string.Equals(verb, "refreshWidgets", StringComparison.Ordinal))
            {
                _sessions.TryGetValue(instanceId, out var current);
                await ShowSelectorCoreAsync(instanceId, current?.Binding, ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(verb, "selectWidget", StringComparison.Ordinal))
            {
                var selection = ReadStringProperty(data, "widget");
                if (WidgetBinding.TryDecode(selection, out var selected))
                {
                    await BindAsync(instanceId, selected, ct).ConfigureAwait(false);
                }
                else
                {
                    await ShowSelectorCoreAsync(instanceId, null, ct).ConfigureAwait(false);
                }

                return;
            }

            var session = await RestoreAsync(instanceId, customState, ct).ConfigureAwait(false);
            if (session is null)
            {
                await ShowSelectorCoreAsync(instanceId, null, ct).ConfigureAwait(false);
                return;
            }

            var context = new JsonObject
            {
                ["verb"] = verb,
                ["surface"] = "windowsWidgets",
            }.ToJsonString();
            await InvokeExtensionAsync(
                () => session.Form.SubmitForm(string.IsNullOrWhiteSpace(data) ? EmptyJson : data, context),
                ct).ConfigureAwait(false);
            session.RefreshForm();
            UpdateSession(session);
            },
            cancellationToken);

    private async Task BindAsync(string instanceId, WidgetBinding binding, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(instanceId, out var current) && current.Binding == binding)
        {
            _selectors.TryRemove(instanceId, out _);
            UpdateSession(current);
            return;
        }

        ShowLoading(instanceId);
        await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var entry = _catalog.Find(binding);
        if (entry is null)
        {
            ShowUnavailable(instanceId, binding, "That extension widget is no longer available.");
            return;
        }

        if (!entry.Definition.AllowMultiple && IsAlreadyBound(binding, instanceId))
        {
            ShowMessage(instanceId, "Already added", "Only one instance of this widget can be added.", binding.Serialize(), "chooseWidget");
            return;
        }

        IWidgetContent? widget;
        try
        {
            widget = await _catalog.CreateAsync(binding, instanceId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError($"Failed to create extension widget {binding.WidgetId}", ex);
            widget = null;
        }

        if (widget?.Content is null)
        {
            ShowUnavailable(instanceId, binding, "The extension could not create this widget.");
            return;
        }

        if (_sessions.TryRemove(instanceId, out var previous))
        {
            previous.Dispose();
        }

        var session = new WidgetSession(instanceId, binding, entry.Definition, widget, ScheduleUpdate);
        _sessions[instanceId] = session;
        _selectors.TryRemove(instanceId, out _);
        UpdateSession(session);
    }

    private async Task<WidgetSession?> RestoreAsync(
        string instanceId,
        string? customState,
        CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(instanceId, out var existing))
        {
            return existing;
        }

        customState ??= _platform.GetWidget(instanceId)?.CustomState;
        if (!WidgetBinding.TryParse(customState, out var binding))
        {
            return null;
        }

        await BindAsync(instanceId, binding, cancellationToken).ConfigureAwait(false);
        return _sessions.TryGetValue(instanceId, out var restored) ? restored : null;
    }

    private async Task ShowSelectorCoreAsync(
        string instanceId,
        WidgetBinding? selectedBinding,
        CancellationToken cancellationToken)
    {
        _selectors.TryGetValue(instanceId, out var previousSelector);
        selectedBinding ??= previousSelector?.SelectedBinding;
        if (selectedBinding is null && WidgetBinding.TryParse(_platform.GetWidget(instanceId)?.CustomState, out var persistedBinding))
        {
            selectedBinding = persistedBinding;
        }

        _selectors[instanceId] = new(selectedBinding);
        var customState = selectedBinding?.Serialize() ?? EmptyJson;
        if (previousSelector is null)
        {
            ShowLoading(instanceId, customState);
        }

        try
        {
            await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError("Failed to refresh the widget catalog", ex);
            ShowMessage(instanceId, "Widgets unavailable", "Command Palette could not load extension widgets.", customState, "refreshWidgets");
            return;
        }

        var entries = new List<WidgetCatalogEntry>();
        foreach (var entry in _catalog.Entries)
        {
            var binding = entry.Binding;
            var selected = selectedBinding == binding;
            if (!entry.Definition.AllowMultiple && !selected && IsAlreadyBound(binding, instanceId))
            {
                continue;
            }

            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            ShowMessage(instanceId, "No extension widgets", "Install or enable a Command Palette extension that provides widgets, then refresh.", customState, "refreshWidgets");
            return;
        }

        var choices = new JsonArray();
        foreach (var entry in entries)
        {
            var title = string.IsNullOrWhiteSpace(entry.Definition.Title) ? entry.Definition.WidgetId : entry.Definition.Title;
            var label = string.IsNullOrWhiteSpace(entry.Definition.SourceName) ? title : $"{entry.Definition.SourceName}: {title}";
            choices.Add((JsonNode)new JsonObject
            {
                ["title"] = label,
                ["value"] = entry.Binding.Encode(),
            });
        }

        var data = new JsonObject
        {
            ["choices"] = choices,
            ["value"] = entries.FirstOrDefault(entry => entry.Binding == selectedBinding)?.Binding.Encode() ?? string.Empty,
        };
        _platform.Update(new(
            instanceId,
            SelectorTemplate,
            data.ToJsonString(),
            customState));
    }

    private bool IsAlreadyBound(WidgetBinding binding, string exceptInstanceId)
    {
        if (_sessions.Values.Any(session =>
            !string.Equals(session.InstanceId, exceptInstanceId, StringComparison.Ordinal) &&
            session.Binding == binding))
        {
            return true;
        }

        return _platform.GetWidgets().Any(info =>
            !string.Equals(info.WidgetId, exceptInstanceId, StringComparison.Ordinal) &&
            WidgetBinding.TryParse(info.CustomState, out var existing) &&
            existing == binding);
    }

    private void ScheduleUpdate(WidgetSession session)
    {
        var next = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        if (_pendingUpdates.TryGetValue(session.InstanceId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _pendingUpdates[session.InstanceId] = next;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(UpdateDebounce, next.Token).ConfigureAwait(false);
                await ExecuteSerializedAsync(
                    session.InstanceId,
                    _ =>
                    {
                        if (_sessions.TryGetValue(session.InstanceId, out var current) && ReferenceEquals(current, session))
                        {
                            current.RefreshForm();
                            UpdateSession(current);
                        }

                        return Task.CompletedTask;
                    },
                    next.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_pendingUpdates.TryRemove(new KeyValuePair<string, CancellationTokenSource>(session.InstanceId, next)))
                {
                    next.Dispose();
                }
            }
        });
    }

    private void UpdateSession(WidgetSession session)
    {
        if (_selectors.ContainsKey(session.InstanceId))
        {
            return;
        }

        if (!SupportsSize(session.Definition, session.Size))
        {
            ShowMessage(
                session.InstanceId,
                "Unsupported size",
                "This extension does not support the current widget size. Resize it or choose another widget.",
                session.Binding.Serialize(),
                "chooseWidget");
            return;
        }

        string template;
        string data;
        try
        {
            template = session.Form.TemplateJson;
            data = string.IsNullOrWhiteSpace(session.Form.DataJson) ? EmptyJson : session.Form.DataJson;
            ValidatePayload(template, nameof(IFormContent.TemplateJson));
            ValidatePayload(data, nameof(IFormContent.DataJson));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Invalid content from extension widget {session.Binding.WidgetId}", ex);
            ShowMessage(
                session.InstanceId,
                "Widget content error",
                "The extension returned content that the Widgets Board could not render.",
                session.Binding.Serialize(),
                "chooseWidget");
            return;
        }

        _platform.Update(new(session.InstanceId, template, data, session.Binding.Serialize()));
    }

    private void ShowLoading(string instanceId, string? customState = null)
    {
        var bindingState = customState ?? (_sessions.TryGetValue(instanceId, out var session)
            ? session.Binding.Serialize()
            : _platform.GetWidget(instanceId)?.CustomState ?? EmptyJson);
        _platform.Update(new(instanceId, LoadingTemplate, EmptyJson, bindingState, IsPlaceholder: true));
    }

    private void ShowUnavailable(string instanceId, WidgetBinding binding, string message) =>
        ShowMessage(instanceId, "Widget unavailable", message, binding.Serialize(), "chooseWidget");

    private void ShowMessage(string instanceId, string title, string message, string customState, string actionVerb)
    {
        _platform.Update(new(
            instanceId,
            MessageTemplate,
            new JsonObject
            {
                ["title"] = title,
                ["message"] = message,
                ["actionTitle"] = actionVerb == "refreshWidgets" ? "Refresh" : "Choose another",
                ["actionVerb"] = actionVerb,
            }.ToJsonString(),
            customState));
    }

    private static void ValidatePayload(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxPayloadLength || JsonNode.Parse(json) is null)
        {
            throw new InvalidDataException($"{propertyName} is missing, invalid, or too large.");
        }
    }

    private static bool SupportsSize(WidgetDefinition definition, string? size) =>
        definition.SupportedSizes.Length == 0 ||
        string.IsNullOrEmpty(size) ||
        definition.SupportedSizes.Any(candidate => string.Equals(candidate.ToString(), size, StringComparison.OrdinalIgnoreCase));

    private static string? ReadStringProperty(string? json, string propertyName)
    {
        try
        {
            return JsonNode.Parse(json ?? EmptyJson)?[propertyName]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static async Task InvokeExtensionAsync(Action action, CancellationToken cancellationToken) =>
        await Task.Run(action, cancellationToken)
            .WaitAsync(ExtensionCallTimeout, cancellationToken)
            .ConfigureAwait(false);

    private static async Task InvokeExtensionAsync(Func<ICommandResult> action, CancellationToken cancellationToken) =>
        await Task.Run(action, cancellationToken)
            .WaitAsync(ExtensionCallTimeout, cancellationToken)
            .ConfigureAwait(false);

    private async Task ExecuteSerializedAsync(
        string instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        var gate = _instanceGates.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await operation(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        foreach (var pending in _pendingUpdates.Values)
        {
            pending.Cancel();
            pending.Dispose();
        }

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        foreach (var gate in _instanceGates.Values)
        {
            gate.Dispose();
        }

        _pendingUpdates.Clear();
        _sessions.Clear();
        _selectors.Clear();
        _instanceGates.Clear();
        _shutdown.Dispose();
    }

    private sealed record SelectorState(WidgetBinding? SelectedBinding);

    private sealed partial class WidgetSession : IDisposable
    {
        private readonly Action<WidgetSession> _changed;
        private readonly TypedEventHandler<object, IPropChangedEventArgs> _widgetChanged;
        private readonly TypedEventHandler<object, IPropChangedEventArgs> _formChanged;
        private bool _disposed;

        public WidgetSession(
            string instanceId,
            WidgetBinding binding,
            WidgetDefinition definition,
            IWidgetContent widget,
            Action<WidgetSession> changed)
        {
            InstanceId = instanceId;
            Binding = binding;
            Definition = definition;
            Widget = widget;
            Form = widget.Content;
            _changed = changed;
            _widgetChanged = (_, args) =>
            {
                if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(IWidgetContent.Content))
                {
                    RefreshForm();
                }

                _changed(this);
            };
            _formChanged = (_, _) => _changed(this);
            Widget.PropChanged += _widgetChanged;
            Form.PropChanged += _formChanged;
        }

        public string InstanceId { get; }

        public WidgetBinding Binding { get; }

        public WidgetDefinition Definition { get; }

        public IWidgetContent Widget { get; }

        public IFormContent Form { get; private set; }

        public string? Size { get; set; }

        public bool IsActive { get; set; }

        public void RefreshForm()
        {
            var next = Widget.Content;
            if (ReferenceEquals(next, Form))
            {
                return;
            }

            Form.PropChanged -= _formChanged;
            Form = next;
            Form.PropChanged += _formChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Widget.PropChanged -= _widgetChanged;
            Form.PropChanged -= _formChanged;
        }
    }

    private const string LoadingTemplate = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.5",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Loading Command Palette widgets...",
                    "wrap": true,
                    "weight": "Bolder"
                }
            ]
        }
        """;

    private const string SelectorTemplate = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.5",
            "body": [
                {
                    "type": "Input.ChoiceSet",
                    "id": "widget",
                    "label": "Widget",
                    "style": "compact",
                    "isMultiSelect": false,
                    "isRequired": true,
                    "errorMessage": "Choose a widget.",
                    "placeholder": "Choose a widget",
                    "value": "${value}",
                    "choices": [
                        {
                            "$data": "${choices}",
                            "title": "${title}",
                            "value": "${value}"
                        }
                    ]
                }
            ],
            "actions": [
                {
                    "type": "Action.Execute",
                    "title": "Use widget",
                    "verb": "selectWidget",
                    "associatedInputs": "auto"
                }
            ]
        }
        """;

    private const string MessageTemplate = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.5",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "${title}",
                    "weight": "Bolder",
                    "wrap": false,
                    "maxLines": 1
                },
                {
                    "type": "TextBlock",
                    "text": "${message}",
                    "wrap": true,
                    "maxLines": 2,
                    "spacing": "Small"
                }
            ],
            "actions": [
                {
                    "type": "Action.Execute",
                    "title": "${actionTitle}",
                    "verb": "${actionVerb}"
                }
            ]
        }
        """;
}
