// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Services.PythonScripts;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public sealed class PasteFormatExecutor(
    IKernelService kernelService,
    ICustomActionTransformService customActionTransformService,
    IPythonScriptService pythonScriptService,
    IPythonScriptTrustService pythonScriptTrustService) : IPasteFormatExecutor
{
    private readonly IKernelService _kernelService = kernelService;
    private readonly ICustomActionTransformService _customActionTransformService = customActionTransformService;
    private readonly IPythonScriptService _pythonScriptService = pythonScriptService;
    private readonly IPythonScriptTrustService _pythonScriptTrustService = pythonScriptTrustService;

    public async Task<DataPackage> ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source, CancellationToken cancellationToken, IProgress<double> progress)
    {
        if (!pasteFormat.IsEnabled)
        {
            return null;
        }

        var format = pasteFormat.Format;

        WriteTelemetry(format, source);

        var clipboardData = Clipboard.GetContent();

        // PythonScript must NOT run inside Task.Run: the trust confirmation (ContentDialog)
        // requires the UI (XAML) thread and will throw if called from a thread-pool thread.
        // Python script execution is fully async (process.WaitForExitAsync), so it is safe
        // to await it directly without wrapping in Task.Run.
        if (format == PasteFormats.PythonScript)
        {
            return await ExecutePythonScriptAsync(pasteFormat.Prompt, clipboardData, cancellationToken, progress);
        }

        // Run on thread-pool; although we use Async routines consistently, some actions still occasionally take a long time without yielding.
        return await Task.Run(async () =>
            pasteFormat.Format switch
            {
                PasteFormats.KernelQuery => await _kernelService.TransformClipboardAsync(pasteFormat.Prompt, clipboardData, pasteFormat.IsSavedQuery, cancellationToken, progress),
                PasteFormats.CustomTextTransformation => DataPackageHelpers.CreateFromText((await _customActionTransformService.TransformAsync(pasteFormat.Prompt, await clipboardData.GetTextOrHtmlTextAsync(), await clipboardData.GetImageAsPngBytesAsync(), cancellationToken, progress))?.Content ?? string.Empty),
                _ => await TransformHelpers.TransformAsync(format, clipboardData, cancellationToken, progress),
            });
    }

    private async Task<DataPackage> ExecutePythonScriptAsync(
        string scriptPath,
        DataPackageView clipboardData,
        CancellationToken cancellationToken,
        IProgress<double> progress)
    {
        // Security: ensure the script is trusted before executing.
        if (!_pythonScriptTrustService.IsTrusted(scriptPath))
        {
            var hash = _pythonScriptTrustService.ComputeHash(scriptPath);
            var approved = await _pythonScriptTrustService.RequestTrustAsync(scriptPath, hash);

            if (!approved)
            {
                throw new OperationCanceledException("User declined to trust the Python script.");
            }

            _pythonScriptTrustService.StoreTrust(scriptPath, hash);
        }

        var metadata = _pythonScriptService.ReadMetadata(scriptPath);
        var detectedFormat = await clipboardData.GetAvailableFormatsAsync();

        if (string.Equals(metadata.Platform, "linux", StringComparison.OrdinalIgnoreCase))
        {
            return await _pythonScriptService.ExecuteWslScriptAsync(scriptPath, clipboardData, detectedFormat, cancellationToken, progress);
        }
        else
        {
            // Windows mode: script modifies the clipboard in-process; we return the updated clipboard.
            await _pythonScriptService.ExecuteWindowsScriptAsync(scriptPath, detectedFormat, cancellationToken, progress);

            // Re-read clipboard after script has run.
            return Clipboard.GetContent() is { } updatedView
                ? await DataPackageFromViewAsync(updatedView)
                : new DataPackage();
        }
    }

    private static async Task<DataPackage> DataPackageFromViewAsync(DataPackageView view)
    {
        var pkg = new DataPackage();

        if (view.Contains(StandardDataFormats.Text))
        {
            pkg.SetText(await view.GetTextAsync());
        }
        else if (view.Contains(StandardDataFormats.Html))
        {
            pkg.SetHtmlFormat(await view.GetHtmlFormatAsync());
        }
        else if (view.Contains(StandardDataFormats.StorageItems))
        {
            var items = await view.GetStorageItemsAsync();
            pkg.SetStorageItems(items);
        }
        else if (view.Contains(StandardDataFormats.Bitmap))
        {
            var bitmap = await view.GetBitmapAsync();
            pkg.SetBitmap(bitmap);
        }

        return pkg;
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
