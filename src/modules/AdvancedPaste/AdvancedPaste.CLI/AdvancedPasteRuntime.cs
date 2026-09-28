// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli;

internal sealed class AdvancedPasteRuntime(IPasteFormatExecutor executor, IUserSettings settings) : IAdvancedPasteRuntime
{
    internal static readonly IReadOnlyDictionary<string, PasteFormats> BuiltInActions =
        new Dictionary<string, PasteFormats>(StringComparer.OrdinalIgnoreCase)
        {
            ["plain-text"] = PasteFormats.PlainText,
            ["markdown"] = PasteFormats.Markdown,
            ["json"] = PasteFormats.Json,
            ["fix-spelling-and-grammar"] = PasteFormats.FixSpellingAndGrammar,
            ["image-to-text"] = PasteFormats.ImageToText,
            ["paste-as-txt-file"] = PasteFormats.PasteAsTxtFile,
            ["paste-as-png-file"] = PasteFormats.PasteAsPngFile,
            ["paste-as-html-file"] = PasteFormats.PasteAsHtmlFile,
            ["transcode-to-mp3"] = PasteFormats.TranscodeToMp3,
            ["transcode-to-mp4"] = PasteFormats.TranscodeToMp4,
        };

    private readonly IPasteFormatExecutor _executor = executor;
    private readonly IUserSettings _settings = settings;

    public IReadOnlyList<CliActionDescriptor> GetActions()
        => BuiltInActions.Keys
            .Select(name => new CliActionDescriptor(name, "built-in", null, RequiresPrompt: false))
            .Append(new CliActionDescriptor("paste-with-ai", "built-in", null, RequiresPrompt: true))
            .Concat(_settings.CustomActions.Select(action => new CliActionDescriptor(action.Name, "custom", action.Id, RequiresPrompt: false)))
            .ToArray();

    public async Task<DataPackage> ExecuteAsync(CliActionRequest request, DataPackageView input, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        var formats = await input.GetAvailableFormatsAsync();
        var pasteFormat = ResolvePasteFormat(request, formats);
        if (!pasteFormat.IsEnabled)
        {
            throw new InvalidOperationException("The selected action does not support the supplied input or its configured AI provider is unavailable.");
        }

        return await _executor.ExecutePasteFormatAsync(pasteFormat, input, PasteActionSource.CommandLine, cancellationToken, progress);
    }

    private PasteFormat ResolvePasteFormat(CliActionRequest request, ClipboardFormat formats)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomAction))
        {
            EnsureAIEnabled();
            var customAction = ResolveCustomAction(request.CustomAction);
            var providerId = string.IsNullOrWhiteSpace(request.ProviderId) ? customAction.ProviderId : request.ProviderId;
            EnsureProviderAllowed(providerId);
            return PasteFormat.CreateCustomAIFormat(
                GetCustomAIFormat(providerId),
                customAction.Name,
                customAction.Prompt,
                isSavedQuery: true,
                formats,
                isAIServiceEnabled: true,
                providerId);
        }

        if (string.Equals(request.Action, "paste-with-ai", StringComparison.OrdinalIgnoreCase))
        {
            EnsureAIEnabled();
            var providerId = request.ProviderId;
            EnsureProviderAllowed(providerId);
            return PasteFormat.CreateCustomAIFormat(
                GetCustomAIFormat(providerId),
                "Command line prompt",
                request.Prompt ?? string.Empty,
                isSavedQuery: false,
                formats,
                isAIServiceEnabled: true,
                providerId);
        }

        if (!BuiltInActions.TryGetValue(request.Action ?? string.Empty, out var format))
        {
            throw new ArgumentException("Unsupported action.", nameof(request));
        }

        var builtInProviderId = format == PasteFormats.FixSpellingAndGrammar && string.IsNullOrWhiteSpace(request.ProviderId)
            ? _settings.FixSpellingAndGrammarProviderId
            : request.ProviderId;
        if (PasteFormat.MetadataDict[format].RequiresAIService)
        {
            EnsureAIEnabled();
            EnsureProviderAllowed(builtInProviderId);
        }

        return PasteFormat.CreateStandardFormat(format, formats, isAIServiceEnabled: true, resourceLoader: value => value, builtInProviderId);
    }

    private void EnsureAIEnabled()
    {
        if (!_settings.IsAIEnabled)
        {
            throw new InvalidOperationException("AI actions are disabled in Advanced Paste settings.");
        }
    }

    private AdvancedPasteCustomAction ResolveCustomAction(string value)
    {
        AdvancedPasteCustomAction? action = null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            action = _settings.CustomActions.FirstOrDefault(candidate => candidate.Id == id);
        }

        action ??= _settings.CustomActions.FirstOrDefault(candidate => string.Equals(candidate.Name, value, StringComparison.OrdinalIgnoreCase));
        return action ?? throw new ArgumentException($"Custom action '{value}' was not found.", nameof(value));
    }

    private PasteFormats GetCustomAIFormat(string? providerId)
        => _settings.IsAIEnabled && AdvancedAIProviderResolver.TryResolveAdvancedProvider(_settings.PasteAIConfiguration, providerId, out _)
            ? PasteFormats.KernelQuery
            : PasteFormats.CustomTextTransformation;

    private void EnsureProviderAllowed(string? providerId)
    {
        var configuration = _settings.PasteAIConfiguration;
        var provider = string.IsNullOrWhiteSpace(providerId)
            ? configuration?.ActiveProvider ?? configuration?.Providers?.FirstOrDefault()
            : configuration?.Providers?.FirstOrDefault(candidate => string.Equals(candidate.Id, providerId, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            throw new InvalidOperationException("No AI provider is configured.");
        }

        if (!AdvancedPastePolicy.IsProviderAllowed(provider))
        {
            throw new InvalidOperationException("The selected AI provider is disabled by policy.");
        }
    }
}
