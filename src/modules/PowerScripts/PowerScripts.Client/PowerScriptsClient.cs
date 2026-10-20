// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>
/// The one-stop client for consuming PowerScripts from another module (or any .NET 8+ tool). It hides
/// host discovery, process spawning, argument building, and JSON parsing behind a few typed methods,
/// so a new consuming module never has to re-implement that boilerplate.
///
/// <para>Typical use:</para>
/// <code>
/// var client = new PowerScriptsClient();
/// if (!client.IsAvailable) return;                       // PowerScripts not installed
/// foreach (var s in client.ListActions())                // no-input action scripts
/// {
///     // show s.Name / s.Description in your UI...
/// }
/// client.Run("whats-my-ip");                              // run a script by id
/// </code>
///
/// <para>
/// The client speaks only the process + JSON contract of <c>PowerScripts.Host.exe</c>; it does not
/// reference the PowerScripts engine, so consuming modules stay fully decoupled and every run still
/// goes through the host's single, never-elevated, trust-gated execution path.
/// </para>
/// </summary>
public sealed class PowerScriptsClient
{
    private readonly string? _hostPath;
    private readonly string? _scriptsRoot;

    /// <summary>
    /// Creates a client. Pass <paramref name="hostPath"/> to point at a specific
    /// <c>PowerScripts.Host.exe</c> (otherwise it is auto-discovered), and <paramref name="scriptsRoot"/>
    /// to scan a specific folder (otherwise the host's configured/default root is used — handy for
    /// tests and demos where you want to choose the folder).
    /// </summary>
    public PowerScriptsClient(string? hostPath = null, string? scriptsRoot = null)
    {
        _hostPath = string.IsNullOrWhiteSpace(hostPath) ? ResolveHostPath() : hostPath;
        _scriptsRoot = string.IsNullOrWhiteSpace(scriptsRoot) ? null : scriptsRoot;
    }

    /// <summary>The resolved host path, or null when the host could not be found.</summary>
    public string? HostPath => _hostPath;

    /// <summary>True when a usable host executable was found.</summary>
    public bool IsAvailable => !string.IsNullOrEmpty(_hostPath) && File.Exists(_hostPath);

    /// <summary>
    /// Enumerates every installed script (<c>list --json</c>). Returns an empty list when the host is
    /// unavailable or fails, so a missing/disabled module simply surfaces no scripts.
    /// </summary>
    public IReadOnlyList<PowerScriptInfo> List()
    {
        if (!IsAvailable)
        {
            return [];
        }

        try
        {
            var psi = CreateStartInfo(redirectStdOut: true);
            psi.ArgumentList.Add("list");
            psi.ArgumentList.Add("--json");
            AppendRoot(psi);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return [];
            }

            var jsonTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                TryKill(process);
                return [];
            }

            return PowerScriptInfo.ParseList(jsonTask.GetAwaiter().GetResult());
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Enumerates only the action scripts (those that take no input) — e.g. Keyboard Manager hotkeys or Command Palette commands.</summary>
    public IReadOnlyList<PowerScriptInfo> ListActions() =>
        List().Where(s => s.IsAction).ToList();

    /// <summary>Enumerates the scripts that consume <paramref name="input"/> — e.g. <c>ListAccepting(PowerScriptIO.Text)</c> when text is on the clipboard.</summary>
    public IReadOnlyList<PowerScriptInfo> ListAccepting(PowerScriptIO input) =>
        List().Where(s => s.Accepts(input)).ToList();

    /// <summary>Enumerates the scripts that produce <paramref name="output"/>.</summary>
    public IReadOnlyList<PowerScriptInfo> ListProducing(PowerScriptIO output) =>
        List().Where(s => s.Produces(output)).ToList();

    /// <summary>Enumerates the file scripts that accept <paramref name="extension"/> (e.g. <c>.md</c>). A <c>*</c> filter matches any extension.</summary>
    public IReadOnlyList<PowerScriptInfo> ListForFileExtension(string extension)
    {
        var ext = NormalizeExtension(extension);
        return List()
            .Where(s => s.Input == PowerScriptIO.Files && MatchesExtension(s, ext))
            .ToList();
    }

    /// <summary>Enumerates the file scripts that accept every one of the given <paramref name="paths"/> (by extension).</summary>
    public IReadOnlyList<PowerScriptInfo> ListForFiles(IEnumerable<string> paths)
    {
        var exts = paths.Select(p => NormalizeExtension(Path.GetExtension(p))).Distinct().ToList();
        return List()
            .Where(s => s.Input == PowerScriptIO.Files && exts.All(e => MatchesExtension(s, e)))
            .ToList();
    }

    private static string NormalizeExtension(string extension)
    {
        var ext = extension.Trim().ToLowerInvariant();
        if (ext.Length > 0 && ext != "*" && !ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        return ext;
    }

    private static bool MatchesExtension(PowerScriptInfo script, string ext) =>
        script.InputExtensions.Count == 0 ||
        script.InputExtensions.Any(e =>
            e == "*" ||
            string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Runs a script by id (<c>run &lt;id&gt;</c>) and waits for it to finish. Supply
    /// <paramref name="files"/> for file scripts and <paramref name="parameters"/> for named inputs.
    /// Set <paramref name="noConsent"/> to refuse (rather than prompt) an untrusted script — pre-approve it
    /// with <see cref="TrustApprove"/> first.
    /// </summary>
    public PowerScriptRunResult Run(
        string id,
        IEnumerable<string>? files = null,
        IReadOnlyDictionary<string, string?>? parameters = null,
        bool noConsent = false,
        int timeoutMs = 60000)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsAvailable)
        {
            return new PowerScriptRunResult { ExitCode = PowerScriptsProtocol.ExitCode.Usage };
        }

        var psi = CreateStartInfo(redirectStdOut: true, redirectStdErr: true);
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(id);

        if (files is not null)
        {
            var fileList = files.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
            if (fileList.Count > 0)
            {
                psi.ArgumentList.Add("--files");
                foreach (var file in fileList)
                {
                    psi.ArgumentList.Add(file);
                }
            }
        }

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                psi.ArgumentList.Add("--set");
                psi.ArgumentList.Add($"{key}={value ?? string.Empty}");
            }
        }

        if (noConsent)
        {
            psi.ArgumentList.Add("--no-consent");
        }

        AppendRoot(psi);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start the PowerScripts host.");

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                TryKill(process);
                return new PowerScriptRunResult { ExitCode = PowerScriptsProtocol.ExitCode.Timeout };
            }

            return new PowerScriptRunResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdOut.GetAwaiter().GetResult(),
                StdErr = stdErr.GetAwaiter().GetResult(),
            };
        }
        catch (Exception)
        {
            return new PowerScriptRunResult { ExitCode = PowerScriptsProtocol.ExitCode.Error };
        }
    }

    /// <summary>
    /// Runs a script as a data transform (<c>transform &lt;id&gt;</c>): serializes
    /// <paramref name="input"/> to the host's stdin and parses the JSON result from stdout. Use this
    /// for clipboard-style "data in, data out" scripts (the Advanced Paste pattern).
    /// </summary>
    public async Task<PowerScriptTransformResult> TransformAsync(
        string id,
        PowerScriptTransformInput input,
        bool noConsent = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(id) || !IsAvailable)
        {
            return new PowerScriptTransformResult { ExitCode = PowerScriptsProtocol.ExitCode.Usage };
        }

        var psi = CreateStartInfo(redirectStdIn: true, redirectStdOut: true, redirectStdErr: true);
        psi.ArgumentList.Add("transform");
        psi.ArgumentList.Add(id);
        if (noConsent)
        {
            psi.ArgumentList.Add("--no-consent");
        }

        AppendRoot(psi);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the PowerScripts host.");

        try
        {
            await process.StandardInput.WriteAsync(SerializeInput(input).AsMemory(), cancellationToken);
            process.StandardInput.Close();

            // Drain stdout and stderr concurrently: the host writes all of stderr before any stdout, so
            // reading only one first can deadlock on a chatty script.
            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return ParseTransformResult(await stdOutTask, await stdErrTask, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Pre-approves a script's current content so it can later run non-interactively
    /// (<c>trust approve &lt;id&gt;</c>). Do this when assigning a script to a hotkey or other silent
    /// trigger — that assignment is itself the user's consent.
    /// </summary>
    public bool TrustApprove(string id) => RunTrustVerb("approve", id);

    /// <summary>Revokes approval for a script (<c>trust revoke &lt;id&gt;</c>), so the next run re-prompts.</summary>
    public bool TrustRevoke(string id) => RunTrustVerb("revoke", id);

    private bool RunTrustVerb(string verb, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsAvailable)
        {
            return false;
        }

        try
        {
            var psi = CreateStartInfo();
            psi.ArgumentList.Add("trust");
            psi.ArgumentList.Add(verb);
            psi.ArgumentList.Add(id);
            AppendRoot(psi);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == PowerScriptsProtocol.ExitCode.Ok;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private ProcessStartInfo CreateStartInfo(
        bool redirectStdIn = false,
        bool redirectStdOut = false,
        bool redirectStdErr = false) => new()
        {
            FileName = _hostPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStdIn,
            RedirectStandardOutput = redirectStdOut,
            RedirectStandardError = redirectStdErr,
            StandardOutputEncoding = redirectStdOut ? Encoding.UTF8 : null,
            StandardErrorEncoding = redirectStdErr ? Encoding.UTF8 : null,
        };

    private void AppendRoot(ProcessStartInfo psi)
    {
        if (_scriptsRoot is not null)
        {
            psi.ArgumentList.Add("--root");
            psi.ArgumentList.Add(_scriptsRoot);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    private static string SerializeInput(PowerScriptTransformInput input)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            WriteString(writer, "text", input.Text);
            WriteString(writer, "html", input.Html);
            WriteString(writer, "image_path", input.ImagePath);
            WriteString(writer, "audio_path", input.AudioPath);
            WriteString(writer, "video_path", input.VideoPath);

            if (input.FilePaths is { Count: > 0 })
            {
                writer.WriteStartArray("file_paths");
                foreach (var path in input.FilePaths)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();
            }

            if (input.Params is { Count: > 0 })
            {
                writer.WriteStartObject("params");
                foreach (var (key, value) in input.Params)
                {
                    writer.WriteString(key, value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static PowerScriptTransformResult ParseTransformResult(string stdOut, string stdErr, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(stdOut))
        {
            return new PowerScriptTransformResult { ExitCode = exitCode, StdErr = stdErr };
        }

        try
        {
            using var document = JsonDocument.Parse(stdOut);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new PowerScriptTransformResult { ExitCode = exitCode, StdErr = stdErr };
            }

            return new PowerScriptTransformResult
            {
                Text = ReadString(root, "text"),
                Html = ReadString(root, "html"),
                ImagePath = ReadString(root, "image_path"),
                AudioPath = ReadString(root, "audio_path"),
                VideoPath = ReadString(root, "video_path"),
                FilePaths = ReadStringArray(root, "file_paths"),
                ExitCode = exitCode,
                StdErr = stdErr,
            };
        }
        catch (JsonException)
        {
            return new PowerScriptTransformResult { ExitCode = exitCode, StdErr = stdErr };
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (text is not null)
                {
                    items.Add(text);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Resolves <c>PowerScripts.Host.exe</c>, or null when it cannot be found. Search order matches
    /// every existing surface: the <c>POWERSCRIPTS_HOST</c> override, next to the calling app, a
    /// <c>PowerScripts</c> subfolder, the per-user module directory, then (for in-repo dev builds) a
    /// walk up to the host project's <c>bin\Debug|Release</c> output.
    /// </summary>
    public static string? ResolveHostPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(PowerScriptsProtocol.HostPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var moduleDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "PowerScripts");

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, PowerScriptsProtocol.HostExeName),
            Path.Combine(AppContext.BaseDirectory, "PowerScripts", PowerScriptsProtocol.HostExeName),
            Path.Combine(moduleDir, PowerScriptsProtocol.HostExeName),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var hostBin = Path.Combine(
                    dir.FullName, "src", "modules", "PowerScripts", "PowerScripts.Host", "bin", config);
                if (Directory.Exists(hostBin))
                {
                    try
                    {
                        var found = Directory
                            .EnumerateFiles(hostBin, PowerScriptsProtocol.HostExeName, SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(found))
                        {
                            candidates.Add(found);
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore an unreadable probe directory.
                    }
                }
            }

            dir = dir.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
