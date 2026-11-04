// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
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
    public static async Task RunAsync(int runnerId)
    {
        using var runner = Process.GetProcessById(runnerId);
        using var singleton = new Mutex(false, @"Local\PowerToys.FileExplorerScripts.Catalogue." + runnerId, out var created);
        if (!created)
        {
            return;
        }

        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\PowerToys.FileExplorerScripts.Stop." + runnerId);
        try
        {
            await ShellRegistration.RegisterAsync();
            var client = new PowerScriptsClient("file-explorer");
            string? lastError = null;
            do
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var enabled = ExplorerSettings.IsParentEnabled() && await IsConsumerEnabledAsync(client, timeout.Token);
                    var scripts = enabled ? await client.ListAsync(timeout.Token) : [];
                    var fileScripts = scripts.Where(item => item.Input == PowerScriptIO.Files).Take(1025).ToArray();
                    var showParameters = ExplorerSettings.ShowParameters();
                    WriteCache(writer =>
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
                            writer.WriteString("iconPath", script.IconPath);
                            writer.WriteNumber("min", script.InputMinFiles);
                            if (script.InputMaxFiles is int max)
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
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    });
                    lastError = null;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or Win32Exception or OperationCanceledException)
                {
                    if (lastError != exception.Message)
                    {
                        Logger.LogError("Unable to refresh Explorer PowerScripts catalogue.", exception);
                        lastError = exception.Message;
                    }

                    WriteCache(writer =>
                    {
                        writer.WriteBoolean("enabled", true);
                        writer.WriteString("error", exception.Message);
                        writer.WriteString("more", Strings.Get("Failed"));
                        writer.WriteStartArray("scripts");
                        writer.WriteEndArray();
                    });
                }
            }
            while (!runner.HasExited && !await Task.Run(() => stop.WaitOne(TimeSpan.FromSeconds(5))));
        }
        finally
        {
            WriteCache(writer =>
            {
                writer.WriteBoolean("enabled", false);
                writer.WriteStartArray("scripts");
                writer.WriteEndArray();
            });
            ShellRegistration.UnregisterClassic();
        }
    }

    private static async Task<bool> IsConsumerEnabledAsync(PowerScriptsClient client, CancellationToken cancellationToken)
    {
        if (!client.IsAvailable)
        {
            throw new FileNotFoundException("The PowerScripts Host is not installed.", client.HostPath);
        }

        var start = new ProcessStartInfo(client.HostPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("consumers");
        start.ArgumentList.Add("list");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to query consumer settings.");
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(await stderr);
            }

            using var document = JsonDocument.Parse(await stdout);
            return document.RootElement.GetProperty("consumers").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == "file-explorer").GetProperty("isEnabled").GetBoolean();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    private static void WriteCache(Action<Utf8JsonWriter> write)
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

            File.Move(temporary, ExplorerSettings.CachePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
