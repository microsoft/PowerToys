// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public sealed class PasteFormatExecutor(IKernelService kernelService, ICustomActionTransformService customActionTransformService, IUserSettings userSettings) : IPasteFormatExecutor
{
    private readonly IKernelService _kernelService = kernelService;
    private readonly ICustomActionTransformService _customActionTransformService = customActionTransformService;
    private readonly IUserSettings _userSettings = userSettings;

    public async Task<DataPackage> ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source, CancellationToken cancellationToken, IProgress<double> progress)
    {
        if (!pasteFormat.IsEnabled)
        {
            return null;
        }

        var format = pasteFormat.Format;

        WriteTelemetry(format, source);

        var clipboardData = Clipboard.GetContent();

        // Run on thread-pool; although we use Async routines consistently, some actions still occasionally take a long time without yielding.
        return await Task.Run(async () =>
            pasteFormat.Format switch
            {
                PasteFormats.KernelQuery => await _kernelService.TransformClipboardAsync(pasteFormat.Prompt, clipboardData, pasteFormat.IsSavedQuery, cancellationToken, progress, pasteFormat.ProviderId),
                PasteFormats.CustomTextTransformation => DataPackageHelpers.CreateFromText((await _customActionTransformService.TransformAsync(pasteFormat.Prompt, await clipboardData.GetTextOrHtmlTextAsync(), await clipboardData.GetImageAsPngBytesAsync(), cancellationToken, progress, providerIdOverride: pasteFormat.ProviderId))?.Content ?? string.Empty),
                PasteFormats.FixSpellingAndGrammar => DataPackageHelpers.CreateFromText((await _customActionTransformService.TransformAsync(GetFixSpellingPrompt(), await clipboardData.GetTextOrHtmlTextAsync(), null, cancellationToken, progress, GetFixSpellingSystemPrompt(), pasteFormat.ProviderId))?.Content ?? string.Empty),
                _ => await TransformHelpers.TransformAsync(format, clipboardData, cancellationToken, progress),
            });
    }

    private static void WriteTelemetry(PasteFormats format, PasteActionSource source)
    {
        switch (source)
        {
            case PasteActionSource.ContextMenu:
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(format));
                break;

            case PasteActionSource.InAppKeyboardShortcut:
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(format));
                break;

            case PasteActionSource.GlobalKeyboardShortcut:
            case PasteActionSource.PromptBox:
                break; // no telemetry yet for these sources

            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private string GetFixSpellingPrompt()
    {
        var customPrompt = _userSettings.FixSpellingAndGrammarPrompt;
        return string.IsNullOrWhiteSpace(customPrompt) ? AdvancedPasteDefaultPrompts.FixSpellingAndGrammar : customPrompt;
    }

    private string GetFixSpellingSystemPrompt()
    {
        var customSystemPrompt = _userSettings.FixSpellingAndGrammarSystemPrompt;
        return string.IsNullOrWhiteSpace(customSystemPrompt) ? AdvancedPasteDefaultPrompts.FixSpellingAndGrammarSystem : customSystemPrompt;
    }
}
