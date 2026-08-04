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
        string trustedHash;
        try
        {
            trustedHash = _pythonScriptTrustService.ComputeHash(scriptPath);
        }
        catch (System.IO.FileNotFoundException)
        {
            throw new PasteActionException(
                string.Format(System.Globalization.CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptNotFound"), scriptPath),
                new System.IO.FileNotFoundException(null, scriptPath));
        }

        // Trust covers the entry script and all Python helpers beneath its directory.
        if (!_pythonScriptTrustService.IsTrusted(scriptPath, trustedHash))
        {
            var approved = await _pythonScriptTrustService.RequestTrustAsync(scriptPath, trustedHash);

            if (!approved)
            {
                throw new OperationCanceledException("User declined to trust the Python script.");
            }

            _pythonScriptTrustService.StoreTrust(scriptPath, trustedHash);
        }

        var scriptRoot = System.IO.Path.GetDirectoryName(scriptPath)
            ?? throw new InvalidOperationException("The Python script path has no parent directory.");
        var snapshotDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerToys-AdvancedPaste", System.Guid.NewGuid().ToString("N"));
        var snapshotScriptPath = System.IO.Path.Combine(snapshotDirectory, System.IO.Path.GetRelativePath(scriptRoot, scriptPath));

        try
        {
            foreach (var sourceFile in System.IO.Directory.EnumerateFiles(scriptRoot, "*.py", System.IO.SearchOption.AllDirectories))
            {
                var destinationFile = System.IO.Path.Combine(snapshotDirectory, System.IO.Path.GetRelativePath(scriptRoot, sourceFile));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationFile)!);
                System.IO.File.Copy(sourceFile, destinationFile);
            }

            if (!string.Equals(_pythonScriptTrustService.ComputeHash(snapshotScriptPath), trustedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The Python script changed while it was being prepared for execution. Please try again.");
            }

            var metadata = _pythonScriptService.ReadMetadata(snapshotScriptPath);

            if (metadata is null)
            {
                throw new InvalidOperationException($"Script '{scriptPath}' does not define a valid advanced_paste_from_*_to_*() function.");
            }

            // Pre-flight: check for missing packages and offer to install them.
            var missingPackages = await _pythonScriptService.GetMissingRequirementsAsync(metadata, cancellationToken);
            if (missingPackages.Count > 0)
            {
                var approved = await _pythonScriptTrustService.RequestInstallAsync(metadata.Name, missingPackages);
                if (!approved)
                {
                    throw new OperationCanceledException("User declined to install missing Python packages.");
                }

                await _pythonScriptService.InstallRequirementsAsync(missingPackages, metadata.Platform, cancellationToken);
            }

            var detectedFormat = await clipboardData.GetAvailableFormatsAsync();

            if (metadata.IsV2)
            {
                return await _pythonScriptService.ExecuteScriptAsync(snapshotScriptPath, metadata.Platform, clipboardData, detectedFormat, cancellationToken, progress);
            }

            if (string.Equals(metadata.Platform, "linux", StringComparison.OrdinalIgnoreCase))
            {
                return await _pythonScriptService.ExecuteWslScriptAsync(snapshotScriptPath, clipboardData, detectedFormat, cancellationToken, progress);
            }

            await _pythonScriptService.ExecuteWindowsScriptAsync(snapshotScriptPath, detectedFormat, cancellationToken, progress);
            return Clipboard.GetContent() is { } updatedView
                ? await DataPackageFromViewAsync(updatedView)
                : new DataPackage();
        }
        finally
        {
            try
            {
                System.IO.Directory.Delete(snapshotDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task<DataPackage> DataPackageFromViewAsync(DataPackageView view)
    {
        var pkg = new DataPackage();

        if (view.Contains(StandardDataFormats.Text))
        {
            pkg.SetText(await view.GetTextAsync());
        }

        if (view.Contains(StandardDataFormats.Html))
        {
            pkg.SetHtmlFormat(await view.GetHtmlFormatAsync());
        }

        if (view.Contains(StandardDataFormats.StorageItems))
        {
            var items = await view.GetStorageItemsAsync();
            pkg.SetStorageItems(items);
        }

        if (view.Contains(StandardDataFormats.Bitmap))
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
