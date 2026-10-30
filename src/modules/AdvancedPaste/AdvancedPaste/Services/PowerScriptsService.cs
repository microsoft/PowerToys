// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using PowerScripts.Client;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Services;

internal static class PowerScriptsService
{
    internal const string PreserveFilesProperty = "AdvancedPaste.PowerScripts.PreserveFiles";

    public static async Task<IReadOnlyList<PowerScriptInfo>> GetAdvancedPasteScriptsAsync()
    {
        var client = new PowerScriptsClient();
        if (!IsPowerScriptsEnabled() || !client.IsAvailable)
        {
            return [];
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var scripts = await client.ListAsync(timeout.Token);
        return scripts.Where(script => IsSupportedInput(script.Input) && IsSupportedOutput(script.Output)).ToArray();
    }

    internal static IReadOnlyList<PowerScriptInfo> GetApplicableScripts(
        IEnumerable<PowerScriptInfo> scripts,
        bool enabled,
        PowerScriptClipboard clipboard)
    {
        if (!enabled)
        {
            return [];
        }

        return scripts.Where(script => IsSupportedInput(script.Input) && CanUseClipboard(script, clipboard)).ToArray();
    }

    private static bool IsSupportedInput(PowerScriptIO input) =>
        input is PowerScriptIO.Text or PowerScriptIO.Files or PowerScriptIO.Image or PowerScriptIO.Audio;

    internal static bool IsSupportedOutput(PowerScriptIO output) =>
        output is PowerScriptIO.Text or PowerScriptIO.Files or PowerScriptIO.Image or PowerScriptIO.Audio;

    internal static bool CanUseClipboard(PowerScriptInfo script, PowerScriptClipboard clipboard)
    {
        if (!IsSupportedOutput(script.Output))
        {
            return false;
        }

        return script.Input switch
        {
            PowerScriptIO.None => true,
            PowerScriptIO.Text => clipboard.HasText,
            PowerScriptIO.Files => script.AcceptsFiles(clipboard.Files),
            PowerScriptIO.Image or PowerScriptIO.Audio => CanUseMedia(script, clipboard),
            _ => false,
        };
    }

    private static bool CanUseMedia(PowerScriptInfo script, PowerScriptClipboard clipboard)
    {
        var (array, minimum, maximum, mimeTypes) = MediaContract(script);
        var count = clipboard.Files.Count > 0 ? clipboard.Files.Count : clipboard.HasBitmap && script.Input == PowerScriptIO.Image ? 1 : 0;
        if (count == 0 || count < minimum || count > maximum || (!array && count != 1))
        {
            return false;
        }

        return clipboard.Files.Count > 0
            ? clipboard.Files.All(path => mimeTypes.Contains(MimeType(path), StringComparer.Ordinal))
            : mimeTypes.Contains("image/png", StringComparer.Ordinal) || mimeTypes.Contains("image/jpeg", StringComparer.Ordinal);
    }

    internal static (bool Array, int Minimum, int Maximum, IReadOnlyList<string> MimeTypes) MediaContract(PowerScriptInfo script)
    {
        var field = script.Input == PowerScriptIO.Image ? "image" : "audio";
        var primary = script.InputSchema!.Value.GetProperty("properties").GetProperty(field);
        var array = primary.GetProperty("type").GetString() == "array";
        var minimum = array && primary.TryGetProperty("minItems", out var min) ? min.GetInt32() : 1;
        var maximum = array && primary.TryGetProperty("maxItems", out var max) ? Math.Min(max.GetInt32(), 64) : array ? 64 : 1;
        var item = array ? primary.GetProperty("items") : primary;
        return (array, minimum, maximum, item.GetProperty("properties").GetProperty("mimeType").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    internal static string MimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".flac" => "audio/flac",
        _ => string.Empty,
    };

    internal static Dictionary<string, JsonElement> CollectParameters(
        IReadOnlyList<PowerScriptParameter> parameters,
        IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (!values.TryGetValue(parameter.Name, out var value))
            {
                if (parameter.IsRequired && parameter.Default is null)
                {
                    throw new ArgumentException(parameter.Label ?? parameter.Name);
                }

                continue;
            }

            if (parameter.Options.Count > 0 && !parameter.Options.Contains(value, StringComparer.Ordinal))
            {
                throw new ArgumentException(parameter.Label ?? parameter.Name);
            }

            switch (parameter.Type)
            {
                case "bool":
                    if (!bool.TryParse(value, out var boolean))
                    {
                        throw new ArgumentException(parameter.Label ?? parameter.Name);
                    }

                    result[parameter.Name] = JsonSerializer.SerializeToElement(boolean);
                    break;
                case "int":
                    if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ||
                        (parameter.Min is int min && number < min) || (parameter.Max is int max && number > max))
                    {
                        throw new ArgumentException(parameter.Label ?? parameter.Name);
                    }

                    result[parameter.Name] = JsonSerializer.SerializeToElement(number);
                    break;
                case "file":
                    if (!File.Exists(value))
                    {
                        throw new ArgumentException(parameter.Label ?? parameter.Name);
                    }

                    result[parameter.Name] = JsonSerializer.SerializeToElement(Path.GetFullPath(value));
                    break;
                default:
                    result[parameter.Name] = JsonSerializer.SerializeToElement(value);
                    break;
            }
        }

        return result;
    }

    public static async Task<DataPackage> TransformAsync(
        PowerScriptInfo script,
        DataPackageView clipboard,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken,
        PowerScriptsClient client = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        var snapshot = await PowerScriptClipboard.ReadAsync(clipboard);
        if (!CanUseClipboard(script, snapshot))
        {
            throw Failure("PowerScriptsClipboardChanged");
        }

        var input = new PowerScriptTransformInput { Parameters = parameters };
        switch (script.Input)
        {
            case PowerScriptIO.Text:
                input.Text = await clipboard.GetTextOrHtmlTextAsync();
                break;
            case PowerScriptIO.Files:
                input.FilePaths = snapshot.Files;
                break;
            case PowerScriptIO.Image:
            case PowerScriptIO.Audio:
                var media = await ReadMediaAsync(script, clipboard, snapshot, token);
                if (script.Input == PowerScriptIO.Image)
                {
                    input.Image = media;
                }
                else
                {
                    input.Audio = media;
                }

                break;
        }

        token.ThrowIfCancellationRequested();
        var result = await (client ?? new PowerScriptsClient()).TransformAsync(script.Id, input, cancellationToken: token);
        if (!result.Succeeded)
        {
            throw Failure("PowerScriptsExecutionFailed", result.StdErr);
        }

        if (result.Text is not null && script.Output == PowerScriptIO.Text)
        {
            if (result.Text.Length == 0)
            {
                throw Failure("PowerScriptsEmptyResult");
            }

            return DataPackageHelpers.CreateFromText(result.Text);
        }

        if (result.FilePaths is { Count: > 0 } paths && script.Output == PowerScriptIO.Files)
        {
            var files = new List<StorageFile>();
            foreach (var path in paths)
            {
                files.Add(await StorageFile.GetFileFromPathAsync(path));
            }

            var output = new DataPackage();
            output.SetStorageItems(files);
            output.Properties[PreserveFilesProperty] = true;
            return output;
        }

        var returnedMedia = script.Output == PowerScriptIO.Image ? result.Image : result.Audio;
        if (returnedMedia is { Items.Count: > 0 })
        {
            if (script.Output == PowerScriptIO.Image && returnedMedia.Items.Count == 1 && !returnedMedia.IsArray)
            {
                var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(returnedMedia.Items[0].Data);
                    await writer.StoreAsync();
                }

                stream.Seek(0);
                var output = new DataPackage();
                output.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                return output;
            }

            return await CreateMediaFilesAsync(returnedMedia, token);
        }

        throw Failure("PowerScriptsEmptyResult");
    }

    private static async Task<PowerScriptMedia> ReadMediaAsync(PowerScriptInfo script, DataPackageView clipboard, PowerScriptClipboard snapshot, CancellationToken token)
    {
        var contract = MediaContract(script);
        var items = new List<PowerScriptMediaItem>();
        long total = 0;
        if (snapshot.Files.Count > 0)
        {
            foreach (var path in snapshot.Files)
            {
                await using var stream = File.OpenRead(path);
                if (stream.Length is <= 0 or > PowerScriptMedia.MaxItemBytes || total + stream.Length > PowerScriptMedia.MaxTotalBytes)
                {
                    throw Failure("PowerScriptsMediaTooLarge");
                }

                var bytes = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(bytes, token);
                total += bytes.Length;
                if (stream.ReadByte() != -1)
                {
                    throw Failure("PowerScriptsMediaTooLarge");
                }

                items.Add(new(MimeType(path), bytes));
            }
        }
        else
        {
            using var bitmapReference = await (await clipboard.GetBitmapAsync()).OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(bitmapReference);
            if ((long)decoder.PixelWidth * decoder.PixelHeight * 4 > 128 * 1024 * 1024)
            {
                throw Failure("PowerScriptsMediaTooLarge");
            }

            using var bitmap = await decoder.GetSoftwareBitmapAsync();
            using var encoded = new InMemoryRandomAccessStream();
            var png = contract.MimeTypes.Contains("image/png", StringComparer.Ordinal);
            var encoder = await BitmapEncoder.CreateAsync(png ? BitmapEncoder.PngEncoderId : BitmapEncoder.JpegEncoderId, encoded);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            if (encoded.Size > PowerScriptMedia.MaxItemBytes)
            {
                throw Failure("PowerScriptsMediaTooLarge");
            }

            using var buffer = new MemoryStream();
            await encoded.AsStreamForRead().CopyToAsync(buffer, token);
            items.Add(new(png ? "image/png" : "image/jpeg", buffer.ToArray()));
        }

        return contract.Array ? new PowerScriptMedia(items) : new PowerScriptMedia(items[0]);
    }

    private static async Task<DataPackage> CreateMediaFilesAsync(PowerScriptMedia media, CancellationToken token)
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PowerToys", "AdvancedPaste", Guid.NewGuid().ToString("N")));
        var paths = new List<string>();
        try
        {
            for (var index = 0; index < media.Items.Count; index++)
            {
                var item = media.Items[index];
                var extension = item.MimeType switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/webp" => ".webp",
                    "audio/wav" => ".wav",
                    "audio/mpeg" => ".mp3",
                    "audio/flac" => ".flac",
                    _ => throw Failure("PowerScriptsEmptyResult"),
                };
                var path = Path.Combine(directory.FullName, $"result-{index + 1}{extension}");
                paths.Add(path);
                await File.WriteAllBytesAsync(path, item.Data, token);
            }

            var files = new List<StorageFile>();
            foreach (var path in paths)
            {
                files.Add(await StorageFile.GetFileFromPathAsync(path));
            }

            var output = new DataPackage();
            output.SetStorageItems(files);
            output.Properties[PreserveFilesProperty] = true;
            _ = CleanupMediaFilesAsync(directory.FullName, paths);
            return output;
        }
        catch
        {
            CleanupMediaFiles(directory.FullName, paths);
            throw;
        }
    }

    private static async Task CleanupMediaFilesAsync(string directory, IReadOnlyList<string> paths)
    {
        await Task.Delay(TimeSpan.FromHours(24));
        CleanupMediaFiles(directory, paths);
    }

    private static void CleanupMediaFiles(string directory, IReadOnlyList<string> paths)
    {
        try
        {
            foreach (var path in paths)
            {
                File.Delete(path);
            }

            Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ManagedCommon.Logger.LogError("Unable to clean up Advanced Paste PowerScripts media files.", ex);
        }
    }

    private static PasteActionException Failure(string resource, string details = null) =>
        new(ResourceLoaderInstance.ResourceLoader.GetString(resource), new InvalidOperationException(resource), details);

    private static bool IsPowerScriptsEnabled()
    {
        if (Environment.GetEnvironmentVariable("POWERSCRIPTS_IGNORE_ENABLED") == "1")
        {
            return true;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys");
        foreach (var (path, moduleConfig) in new[] { (Path.Combine(root, "PowerScripts", "config.json"), true), (Path.Combine(root, "settings.json"), false) })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var configuration = document.RootElement;
            if (!moduleConfig && !configuration.TryGetProperty("enabled", out configuration))
            {
                continue;
            }

            if (configuration.TryGetProperty(moduleConfig ? "enabled" : "PowerScripts", out var enabled) &&
                enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return enabled.GetBoolean();
            }
        }

        return true;
    }
}
