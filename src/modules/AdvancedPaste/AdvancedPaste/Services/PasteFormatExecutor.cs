// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public sealed class PasteFormatExecutor(IKernelService kernelService) : IPasteFormatExecutor
{
    private readonly IKernelService _kernelService = kernelService;

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
                PasteFormats.KernelQuery => await _kernelService.TransformClipboardAsync(pasteFormat.Prompt, clipboardData, pasteFormat.IsSavedQuery, cancellationToken, progress),
                PasteFormats.CustomTextTransformation => await TransformCustomTextAsync(pasteFormat.Prompt, clipboardData, cancellationToken, progress),
                _ => await TransformHelpers.TransformAsync(format, clipboardData, cancellationToken, progress),
            });
    }

    private async Task<DataPackage> TransformCustomTextAsync(string prompt, DataPackageView clipboardData, CancellationToken cancellationToken, IProgress<double> progress)
    {
        var inputText = await clipboardData.GetTextAsync();

        if (_kernelService is KernelServiceBase kernelServiceBase)
        {
            var result = await kernelServiceBase.TransformTextWithSemanticKernelAsync(prompt, inputText, cancellationToken, progress);
            return DataPackageHelpers.CreateFromText(result);
        }

        throw new InvalidOperationException("KernelService is not derived from KernelServiceBase");
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
}
