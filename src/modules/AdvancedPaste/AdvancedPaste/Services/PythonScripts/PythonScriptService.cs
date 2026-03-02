// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Services.PythonScripts;

public sealed class PythonScriptService(IUserSettings userSettings) : IPythonScriptService
{
    private const int MetadataMaxLines = 50;
    private const string PlatformWindows = "windows";
    private const string PlatformLinux = "linux";

    private readonly IUserSettings _userSettings = userSettings;

    public async Task ExecuteWindowsScriptAsync(
        string scriptPath,
        ClipboardFormat detectedFormat,
        CancellationToken cancellationToken,
        IProgress<double> progress)
    {
        ThrowIfNotExists(scriptPath);

        var pythonExe = TryFindPythonExecutable(_userSettings.PythonExecutablePath);
        if (pythonExe is null)
        {
            throw new PasteActionException(
                ResourceLoaderInstance.ResourceLoader.GetString("PythonNotFound"),
                new InvalidOperationException("Python executable not found."));
        }

        var workDir = CreateTempWorkDir();

        try
        {
            var formatName = detectedFormat.ToString().ToLowerInvariant();
            var psi = new ProcessStartInfo(pythonExe, $"\"{scriptPath}\" --format {formatName} --work-dir \"{workDir}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi)
                ?? throw new PasteActionException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptFailed"),
                    new InvalidOperationException("Failed to start Python process."));

            int timeoutMs = _userSettings.PythonScriptTimeoutSeconds * 1000;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                throw new PasteActionException(
                    string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptTimeout"),
                        _userSettings.PythonScriptTimeoutSeconds),
                    new TimeoutException());
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                var errorMsg = stderr.Length > 200 ? stderr[..200] : stderr;
                throw new PasteActionException(errorMsg, new InvalidOperationException($"Script exited with code {process.ExitCode}."));
            }
        }
        finally
        {
            TryDeleteTempDir(workDir);
        }
    }

    public async Task<DataPackage> ExecuteWslScriptAsync(
        string scriptPath,
        DataPackageView clipboardData,
        ClipboardFormat detectedFormat,
        CancellationToken cancellationToken,
        IProgress<double> progress)
    {
        ThrowIfNotExists(scriptPath);

        if (!IsWslAvailable())
        {
            throw new PasteActionException(
                ResourceLoaderInstance.ResourceLoader.GetString("WslNotAvailable"),
                new InvalidOperationException("WSL is not available."));
        }

        var workDir = CreateTempWorkDir();

        try
        {
            var inputPayload = await BuildWslInputPayloadAsync(clipboardData, detectedFormat, workDir, cancellationToken);
            var inputJson = JsonSerializer.Serialize(inputPayload);

            var wslScriptPath = ToWslPath(scriptPath);
            var psi = new ProcessStartInfo("wsl.exe", $"--exec python3 \"{wslScriptPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(psi)
                ?? throw new PasteActionException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptFailed"),
                    new InvalidOperationException("Failed to start WSL process."));

            await process.StandardInput.WriteAsync(inputJson);
            process.StandardInput.Close();

            int timeoutMs = _userSettings.PythonScriptTimeoutSeconds * 1000;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            string stdout;
            string stderr;

            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);
                stdout = await stdoutTask;
                stderr = await stderrTask;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                throw new PasteActionException(
                    string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptTimeout"),
                        _userSettings.PythonScriptTimeoutSeconds),
                    new TimeoutException());
            }

            if (process.ExitCode != 0)
            {
                var errorMsg = stderr.Length > 200 ? stderr[..200] : stderr;
                throw new PasteActionException(errorMsg, new InvalidOperationException($"WSL script exited with code {process.ExitCode}."));
            }

            return await ParseWslOutputAsync(stdout, workDir, cancellationToken);
        }
        finally
        {
            // Delayed cleanup so the target application can finish using the files.
            _ = Task.Run(
                async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
                    TryDeleteTempDir(workDir);
                },
                CancellationToken.None);
        }
    }

    public PythonScriptMetadata ReadMetadata(string scriptPath)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        var description = string.Empty;
        var platform = PlatformWindows;
        var version = "1";
        ClipboardFormat supportedFormats =
            ClipboardFormat.Text | ClipboardFormat.Html |
            ClipboardFormat.Image | ClipboardFormat.Audio |
            ClipboardFormat.Video | ClipboardFormat.File;

        try
        {
            using var reader = new StreamReader(scriptPath, Encoding.UTF8);
            int lineCount = 0;

            while (lineCount < MetadataMaxLines)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                lineCount++;
                line = line.Trim();

                if (!line.StartsWith('#'))
                {
                    continue;
                }

                var tag = ParseTag(line, "@advancedpaste:name");
                if (tag != null)
                {
                    name = tag;
                    continue;
                }

                tag = ParseTag(line, "@advancedpaste:desc");
                if (tag != null)
                {
                    description = tag;
                    continue;
                }

                tag = ParseTag(line, "@advancedpaste:platform");
                if (tag != null)
                {
                    platform = tag.ToLowerInvariant();
                    continue;
                }

                tag = ParseTag(line, "@advancedpaste:version");
                if (tag != null)
                {
                    version = tag;
                    continue;
                }

                tag = ParseTag(line, "@advancedpaste:formats");
                if (tag != null)
                {
                    supportedFormats = ParseFormats(tag);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to read metadata from {scriptPath}", ex);
        }

        return new PythonScriptMetadata(scriptPath, name, description, supportedFormats, platform, version);
    }

    private static string ParseTag(string line, string tag)
    {
        var idx = line.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        return line[(idx + tag.Length)..].Trim();
    }

    private static ClipboardFormat ParseFormats(string value)
    {
        if (string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
        {
            return ClipboardFormat.Text | ClipboardFormat.Html |
                   ClipboardFormat.Image | ClipboardFormat.Audio |
                   ClipboardFormat.Video | ClipboardFormat.File;
        }

        var result = ClipboardFormat.None;
        foreach (var token in value.Split(','))
        {
            result |= token.Trim().ToLowerInvariant() switch
            {
                "text" => ClipboardFormat.Text,
                "html" => ClipboardFormat.Html,
                "image" => ClipboardFormat.Image,
                "audio" => ClipboardFormat.Audio,
                "video" => ClipboardFormat.Video,
                "files" or "file" => ClipboardFormat.File,
                _ => ClipboardFormat.None,
            };
        }

        return result == ClipboardFormat.None
            ? ClipboardFormat.Text | ClipboardFormat.Html |
              ClipboardFormat.Image | ClipboardFormat.Audio |
              ClipboardFormat.Video | ClipboardFormat.File
            : result;
    }

    public IReadOnlyList<PythonScriptMetadata> DiscoverScripts(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var scripts = new List<PythonScriptMetadata>();

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.py", SearchOption.TopDirectoryOnly))
        {
            try
            {
                scripts.Add(ReadMetadata(file));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to discover script {file}", ex);
            }
        }

        return scripts;
    }

    public string TryFindPythonExecutable(string overridePath = null)
    {
        // 1. User-configured override
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        // 2. py.exe (Windows Launcher — installed by the official CPython installer)
        if (TryFindOnSystemPath("py.exe") is string pyExe)
        {
            return pyExe;
        }

        // 3. python.exe anywhere on the merged system + user PATH
        if (TryFindOnSystemPath("python.exe") is string pythonExe)
        {
            return pythonExe;
        }

        // 4. Registry: HKCU and HKLM, standard PythonCore and Anaconda/Continuum vendor keys
        if (TryFindPythonViaRegistry() is string registryExe)
        {
            return registryExe;
        }

        // 5. Common Anaconda / Miniconda default install locations
        if (TryFindInCondaPaths() is string condaExe)
        {
            return condaExe;
        }

        // 6. Windows Store python stub
        var storeExe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "python.exe");
        if (File.Exists(storeExe))
        {
            return storeExe;
        }

        return null;
    }

    /// <summary>
    /// Searches for <paramref name="exeName"/> in the merged system + user PATH read directly
    /// from the registry. This is more reliable than reading the process environment because
    /// Anaconda (and other installers) may have modified PATH after the current process started.
    /// </summary>
    private static string TryFindOnSystemPath(string exeName)
    {
        // Build the effective PATH by merging system PATH and user PATH from the registry,
        // then fall back to the inherited process PATH.
        var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPathEntries(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath))
            {
                return;
            }

            foreach (var entry in rawPath.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    paths.Add(Environment.ExpandEnvironmentVariables(entry));
                }
            }
        }

        try
        {
            using var systemEnvKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
            AddPathEntries(systemEnvKey?.GetValue("Path") as string);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to read system PATH from registry", ex);
        }

        try
        {
            using var userEnvKey = Registry.CurrentUser.OpenSubKey(@"Environment");
            AddPathEntries(userEnvKey?.GetValue("Path") as string);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to read user PATH from registry", ex);
        }

        // Also include the inherited process PATH in case it was set dynamically.
        AddPathEntries(Environment.GetEnvironmentVariable("PATH"));

        foreach (var dir in paths)
        {
            try
            {
                var full = Path.Combine(dir, exeName);
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch (ArgumentException)
            {
                // dir may contain invalid path characters; skip it
            }
        }

        return null;
    }

    /// <summary>
    /// Searches the Windows registry for Python installations.
    /// Checks both HKCU and HKLM, and both the standard CPython vendor key ("PythonCore")
    /// and the Anaconda / Miniconda vendor key ("ContinuumAnalytics").
    /// </summary>
    private static string TryFindPythonViaRegistry()
    {
        // (hive, subKey) pairs to probe, ordered from most-specific to least-specific
        (RegistryKey Hive, string SubKey)[] candidates =
        [
            (Registry.CurrentUser,  @"SOFTWARE\Python\PythonCore"),
            (Registry.LocalMachine, @"SOFTWARE\Python\PythonCore"),
            (Registry.CurrentUser,  @"SOFTWARE\Python\ContinuumAnalytics"),
            (Registry.LocalMachine, @"SOFTWARE\Python\ContinuumAnalytics"),
            (Registry.CurrentUser,  @"SOFTWARE\WOW6432Node\Python\PythonCore"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Python\PythonCore"),
        ];

        foreach (var (hive, subKey) in candidates)
        {
            try
            {
                if (TryFindInRegistryKey(hive, subKey) is string exe)
                {
                    return exe;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to query registry {subKey}", ex);
            }
        }

        return null;
    }

    private static string TryFindInRegistryKey(RegistryKey hive, string subKey)
    {
        using var root = hive.OpenSubKey(subKey);
        if (root is null)
        {
            return null;
        }

        // Vendors may nest by version (e.g. "3.11") or by distribution name then version
        // ("Anaconda3-64\3.11"). Walk up to two levels of sub-keys, newest version first.
        var topNames = root.GetSubKeyNames().OrderByDescending(v => v).ToList();
        foreach (var topName in topNames)
        {
            using var topKey = root.OpenSubKey(topName);
            if (topKey is null)
            {
                continue;
            }

            // Try InstallPath directly under this key (standard CPython layout)
            if (TryReadInstallPath(topKey) is string directExe)
            {
                return directExe;
            }

            // Try one level deeper (Anaconda layout: vendor\distributionName\version\InstallPath)
            var subNames = topKey.GetSubKeyNames().OrderByDescending(v => v).ToList();
            foreach (var subName in subNames)
            {
                using var subVersion = topKey.OpenSubKey(subName);
                if (TryReadInstallPath(subVersion) is string nestedExe)
                {
                    return nestedExe;
                }
            }
        }

        return null;
    }

    private static string TryReadInstallPath(RegistryKey versionKey)
    {
        using var installKey = versionKey?.OpenSubKey("InstallPath");
        if (installKey is null)
        {
            return null;
        }

        var installPath = installKey.GetValue("ExecutablePath") as string
                       ?? installKey.GetValue(string.Empty) as string;

        if (string.IsNullOrEmpty(installPath))
        {
            return null;
        }

        installPath = Environment.ExpandEnvironmentVariables(installPath);

        if (File.Exists(installPath) && installPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return installPath;
        }

        var exe = Path.Combine(installPath, "python.exe");
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>
    /// Checks well-known default installation directories used by Anaconda, Miniconda,
    /// and Miniforge when they are installed without modifying the system PATH.
    /// </summary>
    private static string TryFindInCondaPaths()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] condaDirs =
        [
            Path.Combine(userProfile, "anaconda3"),
            Path.Combine(userProfile, "miniconda3"),
            Path.Combine(userProfile, "miniforge3"),
            Path.Combine(userProfile, "mambaforge"),
            Path.Combine(localAppData, "anaconda3"),
            Path.Combine(localAppData, "miniconda3"),
            Path.Combine(programData, "anaconda3"),
            Path.Combine(programData, "miniconda3"),
            Path.Combine(programData, "miniforge3"),
        ];

        foreach (var dir in condaDirs)
        {
            var exe = Path.Combine(dir, "python.exe");
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return null;
    }

    public bool IsWslAvailable()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(systemRoot, "wsl.exe"));
    }

    internal static string ToWslPath(string windowsPath)
    {
        if (windowsPath.Length < 3 || windowsPath[1] != ':')
        {
            throw new ArgumentException($"Not an absolute Windows path: {windowsPath}", nameof(windowsPath));
        }

        return "/mnt/" + char.ToLowerInvariant(windowsPath[0]) + "/" + windowsPath[3..].Replace('\\', '/');
    }

    internal static string ToWindowsPath(string wslPath)
    {
        if (!wslPath.StartsWith("/mnt/", StringComparison.Ordinal) || wslPath.Length < 7)
        {
            throw new ArgumentException($"Not a /mnt/... WSL path: {wslPath}", nameof(wslPath));
        }

        return char.ToUpperInvariant(wslPath[5]) + ":\\" + wslPath[7..].Replace('/', '\\');
    }

    private async Task<JsonObject> BuildWslInputPayloadAsync(
        DataPackageView clipboardData,
        ClipboardFormat detectedFormat,
        string workDir,
        CancellationToken cancellationToken)
    {
        var obj = new JsonObject
        {
            ["version"] = 2,
            ["format"] = detectedFormat.ToString().ToLowerInvariant(),
            ["work_dir"] = ToWslPath(workDir),
        };

        if (clipboardData.Contains(StandardDataFormats.Text))
        {
            obj["text"] = await clipboardData.GetTextAsync();
        }

        if (clipboardData.Contains(StandardDataFormats.Html))
        {
            obj["html"] = await clipboardData.GetHtmlFormatAsync();
        }

        if (clipboardData.Contains(StandardDataFormats.Bitmap))
        {
            var pngBytes = await clipboardData.GetImageAsPngBytesAsync();
            if (pngBytes != null)
            {
                var inputPng = Path.Combine(workDir, "input.png");
                await File.WriteAllBytesAsync(inputPng, pngBytes, cancellationToken);
                obj["image_path"] = ToWslPath(inputPng);
            }
        }

        if (clipboardData.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await clipboardData.GetStorageItemsAsync();
            var filePathArray = new JsonArray();
            foreach (var item in storageItems)
            {
                filePathArray.Add(JsonValue.Create(ToWslPath(item.Path)));
            }

            obj["file_paths"] = filePathArray;
        }

        return obj;
    }

    private static async Task<DataPackage> ParseWslOutputAsync(
        string stdout,
        string workDir,
        CancellationToken cancellationToken)
    {
        JsonObject outputObj;
        try
        {
            outputObj = JsonSerializer.Deserialize<JsonObject>(stdout)
                ?? throw new FormatException("null root object");
        }
        catch (Exception ex)
        {
            throw new PasteActionException(
                ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptInvalidJson"),
                ex);
        }

        var resultType = outputObj["result_type"]?.GetValue<string>() ?? "text";
        var dataPackage = new DataPackage();

        switch (resultType.ToLowerInvariant())
        {
            case "text":
                var text = outputObj["text"]?.GetValue<string>() ?? string.Empty;
                dataPackage.SetText(text);
                break;

            case "file":
            case "files":
                var filePathsNode = outputObj["file_paths"];
                if (filePathsNode is JsonArray arr)
                {
                    var storageItems = new List<IStorageItem>();
                    foreach (var node in arr)
                    {
                        var wslPath = node?.GetValue<string>();
                        if (!string.IsNullOrEmpty(wslPath))
                        {
                            var winPath = ToWindowsPath(wslPath);
                            storageItems.Add(await StorageFile.GetFileFromPathAsync(winPath));
                        }
                    }

                    if (storageItems.Count > 0)
                    {
                        dataPackage.SetStorageItems(storageItems);
                    }
                }

                break;

            case "image":
                var imagePath = outputObj["image_path"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var winImagePath = ToWindowsPath(imagePath);
                    var file = await StorageFile.GetFileFromPathAsync(winImagePath);
                    dataPackage.SetStorageItems([file]);
                }

                break;

            default:
                var defaultText = outputObj["text"]?.GetValue<string>() ?? string.Empty;
                dataPackage.SetText(defaultText);
                break;
        }

        return dataPackage;
    }

    private static string CreateTempWorkDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ap_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteTempDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to clean up temp dir {dir}", ex);
        }
    }

    private static void ThrowIfNotExists(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            throw new PasteActionException(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    ResourceLoaderInstance.ResourceLoader.GetString("PythonScriptNotFound"),
                    scriptPath),
                new FileNotFoundException("Script file not found.", scriptPath));
        }
    }
}
