// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileExplorerScripts.Core;
using ManagedCommon;
using PowerScripts.Client;

namespace FileExplorerScripts;

internal static class CatalogueWorker
{
    public static async Task RunAsync(int runnerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var singleton = new Mutex(false, @"Local\PowerToys.FileExplorerScripts.Catalogue." + runnerId, out var created);
        if (!created)
        {
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ShellRegistration.RegisterAsync();
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new PowerScriptsClient("file-explorer", reuseHost: true);
            var defaultIconPath = Path.Combine(AppContext.BaseDirectory, "PowerToys.FileExplorerScripts.exe");
            string? lastError = null;
            while (true)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var enabled = ExplorerSettings.IsParentEnabled() && (await client.GetConsumerStateAsync(timeout.Token)).IsEnabled;
                    var scripts = enabled ? await client.ListAsync(timeout.Token) : [];
                    var fileScripts = scripts.Where(item => item.Input is PowerScriptIO.Files or PowerScriptIO.Image or PowerScriptIO.Audio).Take(1025).ToArray();
                    var showParameters = ExplorerSettings.ShowParameters();
                    await WriteCacheAsync(
                        writer =>
                    {
                        writer.WriteBoolean("enabled", enabled);
                        writer.WriteBoolean("showParameters", showParameters);
                        writer.WriteBoolean("truncated", fileScripts.Length > 1024);
                        writer.WriteString("more", Strings.Get("ChooseScript"));
                        writer.WriteString("missingDefaults", Strings.Get("MissingDefaults"));
                        writer.WriteStartArray("scripts");
                        foreach (var script in fileScripts.Take(1024))
                        {
                            writer.WriteStartObject();
                            writer.WriteString("id", script.Id);
                            writer.WriteString("name", script.Name.Length > 128 ? script.Name[..128] : script.Name);
                            writer.WriteString("iconPath", string.IsNullOrEmpty(script.IconPath) ? defaultIconPath : script.IconPath);
                            writer.WriteString("kind", script.Input.ToString().ToLowerInvariant());
                            var media = script.Input == PowerScriptIO.Files ? null : MediaInput.GetContract(script);
                            writer.WriteNumber("min", media?.Minimum ?? script.InputMinFiles);
                            if ((media?.Maximum ?? script.InputMaxFiles) is int max)
                            {
                                writer.WriteNumber("max", max);
                            }

                            writer.WriteBoolean("defaults", ScriptParameters.CanRunWithDefaults(script));
                            writer.WriteStartArray("extensions");
                            foreach (var extension in script.InputExtensions)
                            {
                                writer.WriteStringValue(extension);
                            }

                            writer.WriteEndArray();
                            if (media is not null)
                            {
                                writer.WriteStartArray("mimeTypes");
                                foreach (var mime in media.MimeTypes)
                                {
                                    writer.WriteStringValue(mime);
                                }

                                writer.WriteEndArray();
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                        },
                        cancellationToken);
                    lastError = null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or Win32Exception or OperationCanceledException)
                {
                    if (lastError != exception.Message)
                    {
                        Logger.LogError("Unable to refresh Explorer PowerScripts catalogue.", exception);
                        lastError = exception.Message;
                    }

                    try
                    {
                        await WriteCacheAsync(
                            writer =>
                        {
                            writer.WriteBoolean("enabled", true);
                            writer.WriteString("error", exception.Message);
                            writer.WriteString("more", Strings.Get("Failed"));
                            writer.WriteStartArray("scripts");
                            writer.WriteEndArray();
                            },
                            cancellationToken);
                    }
                    catch (Exception publicationError) when (publicationError is IOException or UnauthorizedAccessException)
                    {
                        Logger.LogError("Unable to publish Explorer PowerScripts recovery status; catalogue refresh will retry.", publicationError);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        finally
        {
            try
            {
                await WriteCacheAsync(
                    writer =>
                {
                    writer.WriteBoolean("enabled", false);
                    writer.WriteStartArray("scripts");
                    writer.WriteEndArray();
                    },
                    CancellationToken.None);
            }
            catch (Exception publicationError) when (publicationError is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Unable to publish Explorer PowerScripts shutdown status.", publicationError);
            }
            finally
            {
                ShellRegistration.UnregisterClassic();
            }
        }
    }

    private static async Task WriteCacheAsync(Action<Utf8JsonWriter> write, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ExplorerSettings.CachePath)!);
        var temporary = ExplorerSettings.CachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("updated", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                write(writer);
                writer.WriteEndObject();
            }

            if (new FileInfo(temporary).Length > 1024 * 1024)
            {
                throw new InvalidOperationException(Strings.Get("CatalogueTooLarge"));
            }

            await CatalogueFile.PublishAsync(temporary, ExplorerSettings.CachePath, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
