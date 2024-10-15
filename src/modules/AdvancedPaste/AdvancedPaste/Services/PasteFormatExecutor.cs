// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public sealed class PasteFormatExecutor(IKernelService kernelService) : IPasteFormatExecutor
{
    private readonly IKernelService _kernelService = kernelService;

    public async Task<DataPackage> ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source)
    {
        if (!pasteFormat.IsEnabled)
        {
            return null;
        }

        WriteTelemetry(pasteFormat.Format, source);

        return await ExecutePasteFormatCoreAsync(pasteFormat.Format, pasteFormat.Prompt, Clipboard.GetContent());
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

    private async Task<DataPackage> ExecutePasteFormatCoreAsync(PasteFormats format, string prompt, DataPackageView clipboardData)
    {
        return format switch
        {
            PasteFormats.KernelQuery => await _kernelService.TransformClipboardAsync(prompt, clipboardData),
            _ => await TransformHelpers.TransformAsync(format, clipboardData),
        };
    }
}
