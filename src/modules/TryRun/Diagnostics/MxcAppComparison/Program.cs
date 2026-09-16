// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Diagnostics;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length is not (3 or 4))
        {
            Console.Error.WriteLine("Usage: MxcAppComparison <Try Run worker directory> <packaged application.exe> <new report directory> [case]");
            return 2;
        }

        var unavailable = RuntimeRequirements.GetUnavailableReason();
        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        var workerDirectory = Path.GetFullPath(args[0]);
        var worker = Path.Combine(workerDirectory, "PowerToys.TryRun.Worker.exe");
        var application = RuntimeFile.Resolve(args[1]);
        var output = Path.GetFullPath(args[2]);
        if (Directory.Exists(output))
        {
            throw new IOException("Use a new report directory; existing reports are never overwritten.");
        }

        foreach (var name in new[] { "Microsoft.Mxc.Sdk.dll", "mxc_ffi.dll" })
        {
            if (Hash(Path.Combine(workerDirectory, name)) != Hash(Path.Combine(AppContext.BaseDirectory, name)))
            {
                throw new InvalidOperationException("The direct driver and worker must use identical MXC binaries: " + name);
            }
        }

        Environment.SetEnvironmentVariable("MXC_FFI_DIR", workerDirectory);

        var capabilities = await new WorkerClient(worker).GetBackendsAsync(CancellationToken.None);
        if (!capabilities.NativeDenialCaptureAvailable)
        {
            throw new InvalidOperationException("This comparison requires native denial capture without elevation.");
        }

        Directory.CreateDirectory(output);
        Write(Path.Combine(output, "runtime.json"), new { Application = application, NativeVersion = MxcSandbox.NativeVersion, Host = Environment.OSVersion.VersionString, SdkSha256 = Hash(Path.Combine(workerDirectory, "Microsoft.Mxc.Sdk.dll")), NativeSha256 = Hash(Path.Combine(workerDirectory, "mxc_ffi.dll")) });
        List<CaseResult> results = [];
        var cases = new[] { "worker", "sdk-identical", "sdk-no-capture", "sdk-default-environment", "sdk-inherit-private-environment", "sdk-package-readonly", "sdk-package-readonly-inherit", "sdk-ime", "cli-identical", "sdk-winver-control" };
        if (args.Length == 4 && !cases.Contains(args[3], StringComparer.Ordinal))
        {
            throw new ArgumentException("Unknown comparison case.");
        }

        foreach (var name in cases.Where(name => args.Length == 3 || args[3] == name))
        {
            Console.WriteLine("Starting " + name);
            using var session = new RunSession();
            var directory = Directory.CreateDirectory(Path.Combine(output, name)).FullName;
            var policy = PolicySettings.Defaults(false);
            policy.Values["timeoutMs"] = "12000";
            policy.Values["captureEnabled"] = "true";
            var input = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 12)
            {
                Kind = WorkloadKind.WindowsApplication,
                ApplicationPath = name == "sdk-winver-control" ? Path.Combine(Environment.SystemDirectory, "winver.exe") : application,
                Policy = policy,
                CaptureDenials = true,
            };
            Write(Path.Combine(directory, "worker-request.json"), JsonSerializer.Deserialize<JsonElement>(RequestCodec.Serialize(input)));
            CaseResult result;
            try
            {
                if (name == "worker")
                {
                    var messages = new ConcurrentQueue<WorkerMessage>();
                    var completed = await new WorkerClient(worker).RunAsync(input, new MessageProgress(messages.Enqueue), CancellationToken.None);
                    Write(Path.Combine(directory, "messages.json"), messages.ToArray());
                    result = new(name, "Try Run worker", completed.ExitCode, completed.TimedOut, null, string.Join('\n', messages.Select(message => message.Text)), null, null);
                }
                else
                {
                    using var snapshot = JsonDocument.Parse(await Describe(worker, input));
                    File.WriteAllText(Path.Combine(directory, "worker-snapshot.json"), snapshot.RootElement.GetRawText());
                    var root = snapshot.RootElement;
                    var request = new SandboxRequest(root.GetProperty("Policy").Deserialize<SandboxPolicy>(Json)!, CommandEncoding.WindowsArgument(input.ApplicationPath!) + " ")
                    {
                        Containment = root.GetProperty("Containment").Deserialize<SandboxContainment>(Json)!,
                        WorkingDirectory = root.GetProperty("WorkingDirectory").GetString(),
                        Environment = root.GetProperty("Environment").Deserialize<Dictionary<string, string>>(Json),
                        InheritDefaultEnvironment = root.GetProperty("InheritDefaultEnvironment").GetBoolean(),
                    };
                    var containment = (ProcessContainerContainment)request.Containment;
                    containment.CaptureDenials!.OutputPath = Path.Combine(directory, "denials.json");
                    if (name == "sdk-no-capture")
                    {
                        containment.CaptureDenials = null;
                    }

                    if (name == "sdk-ime")
                    {
                        containment.Ui!.Ime = true;
                    }

                    if (name == "sdk-default-environment")
                    {
                        request.Environment = null;
                    }

                    if (name.Contains("inherit", StringComparison.Ordinal))
                    {
                        request.InheritDefaultEnvironment = true;
                    }

                    if (name.Contains("package-readonly", StringComparison.Ordinal))
                    {
                        // Only an installed package reported by Windows can add read-only roots.
                        var package = new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
                            .First(package => !package.IsFramework && application.StartsWith(package.InstalledLocation.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
                        request.Policy.Filesystem!.ReadonlyPaths.Add(package.InstalledLocation.Path);
                        request.Policy.Filesystem.ReadonlyPaths.AddRange(package.Dependencies.Select(dependency => dependency.InstalledLocation.Path));
                    }

                    if (containment.AllowDaclMutation != false || containment.LearningMode || containment.Capabilities.Count != 0 || containment.CaptureDenials?.Mode == CaptureDenialsMode.Allow || request.Policy.Network!.AllowOutbound != false || request.Policy.Network.AllowLocalNetwork != false || !request.Policy.Filesystem!.ReadwritePaths.SequenceEqual(new[] { session.WorkingDirectory, session.TemporaryDirectory }))
                    {
                        throw new InvalidOperationException("Comparison must retain deny mode, no network, no DACL mutation, and writes only to its private work/temp directories.");
                    }

                    Write(Path.Combine(directory, "sdk-request.json"), request);
                    result = name == "cli-identical"
                        ? await RunCli(workerDirectory, directory, request)
                        : await RunDirect(name, request);
                }
            }
            catch (Exception exception)
            {
                result = new(name, name == "worker" ? "Try Run worker" : "Direct MXC", null, false, null, string.Empty, null, exception.ToString());
            }

            Write(Path.Combine(directory, "result.json"), result);
            results.Add(result);
            Write(Path.Combine(output, "results.json"), results);
            Console.WriteLine($"{name}: exit={result.ExitHex}, timeout={result.TimedOut}, window={result.WindowObserved}, error={result.Error?.Split('\n')[0]}");
        }

        return results.Any(result => result.Error is not null) ? 1 : 0;
    }

    private static async Task<CaseResult> RunDirect(string name, SandboxRequest request)
    {
        using var process = MxcSandbox.Spawn(request);
        if (process.Warnings.Count > 0)
        {
            process.Kill();
            throw new InvalidOperationException(string.Join(';', process.Warnings));
        }

        process.StandardInput?.Dispose();
        var stdout = Drain(process.StandardOutput);
        var stderr = Drain(process.StandardError);
        var identity = WindowsProcessIdentity.Capture(process.Id);
        var wait = process.WaitAsync();
        var visible = false;
        try
        {
            while (!wait.IsCompleted)
            {
                if (identity is not null && WindowsProcessIdentity.Capture(identity.ProcessId) == identity)
                {
                    try
                    {
                        using var child = Process.GetProcessById((int)identity.ProcessId);
                        if (child.MainWindowHandle != IntPtr.Zero)
                        {
                            visible = true;
                            process.Kill();
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // The child may exit between identity capture and lookup.
                        // Still wait for MXC to finalize its exit code and capture.
                    }
                }

                await Task.Delay(100);
            }

            var result = await wait.WaitAsync(TimeSpan.FromSeconds(30));
            process.Kill();
            return new(name, "Direct MXC .NET SDK", result.ExitCode, result.TimedOut, visible, await stdout + await stderr, process.OutputMetadata?.CaptureDenials?.OutputPath, null);
        }
        finally
        {
            process.Kill();
        }
    }

    private static async Task<CaseResult> RunCli(string workerDirectory, string directory, SandboxRequest request)
    {
        var containment = (ProcessContainerContainment)request.Containment;
        var config = new
        {
            version = "0.8.0-alpha",
            containment = "processcontainer",
            process = new { commandLine = request.Command, cwd = request.WorkingDirectory, env = request.Environment!.Select(pair => pair.Key + "=" + pair.Value).ToArray(), timeout = request.Policy.TimeoutMs },
            filesystem = new { readonlyPaths = request.Policy.Filesystem!.ReadonlyPaths, readwritePaths = request.Policy.Filesystem.ReadwritePaths },
            fallback = new { allowDaclMutation = false },
            network = new { defaultPolicy = "block", allowLocalNetwork = false, enforcementMode = "capabilities" },
            ui = new { disable = false, clipboard = "none", injection = false },
            processContainer = new { leastPrivilege = false, capabilities = Array.Empty<string>(), ui = containment.Ui, captureDenials = containment.CaptureDenials },
        };
        var path = Path.Combine(directory, "cli-config.json");
        Write(path, config);
        var cli = Path.Combine(workerDirectory, "wxc-exec.exe");
        var validation = await RunProcess(cli, ["--config", path, "--dry-run"], null);
        File.WriteAllText(Path.Combine(directory, "dry-run.txt"), validation.Output);
        if (validation.Code != 0)
        {
            throw new InvalidOperationException("CLI rejected the comparison configuration: " + validation.Output);
        }

        var result = await RunProcess(cli, ["--config", path], null);
        File.WriteAllText(Path.Combine(directory, "cli-output.txt"), result.Output);
        return new("cli-identical", "Direct MXC native CLI", result.Code, false, null, result.Output, null, null);
    }

    private static async Task<string> Describe(string worker, ExecutionRequest request)
    {
        var result = await RunProcess(worker, ["--describe-policy"], RequestCodec.Serialize(request));
        if (result.Code != 0)
        {
            throw new InvalidOperationException("Worker policy generation failed: " + result.Output);
        }

        return result.Output;
    }

    private static async Task<(int Code, string Output)> RunProcess(string executable, string[] arguments, string? input)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteLineAsync(input);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            return (process.ExitCode, await stdout + await stderr);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static async Task<string> Drain(Stream? stream)
    {
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        var buffer = new char[4096];
        var output = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
        {
            output.Append(buffer, 0, Math.Min(count, Math.Max(0, 16384 - output.Length)));
        }

        return output.ToString();
    }

    private static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, value.GetType(), Json));

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class MessageProgress(Action<WorkerMessage> callback) : IProgress<WorkerMessage>
    {
        public void Report(WorkerMessage value) => callback(value);
    }
}
