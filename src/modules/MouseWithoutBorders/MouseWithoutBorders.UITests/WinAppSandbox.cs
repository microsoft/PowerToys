// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Management.Deployment;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class WinAppSandbox : ISandboxSession
{
    private const string SandboxPackageFamily = "MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy";
    private static readonly TimeSpan InventoryTimeout = TimeSpan.FromSeconds(30);
    private readonly string runId;
    private readonly Guid instanceId;
    private readonly string controlRoot;
    private readonly string runRoot;
    private readonly string winappPath;
    private readonly string wsbPath;
    private readonly string targetStateRoot;
    private readonly Action saveJournal;
    private readonly TestContext context;
    private readonly List<ProcessIdentity> commandProcesses = [];
    private readonly JsonArray commandLog = [];
    private readonly string recordingPath;
    private readonly string winappSha256;
    private EndpointChannel? guest;
    private WinAppSandboxCommand? worker;
    private WinAppSandboxCommand? recorder;
    private Process? client;
    private string trustedWsbExecutablePath = string.Empty;
    private string? stagedPayload;
    private string? epoch;
    private DateTime bootstrapDeadlineUtc;
    private DateTime hardDeadlineUtc;
    private DateTime lastDiscoveryUtc;
    private DateTime? recordingStartedUtc;
    private bool creationAttempted;
    private bool creationConfirmed;
    private bool ownershipUnconfirmed;
    private bool ownsTargetStateRoot;
    private bool started;
    private bool stopping;
    private bool stopped;
    private bool recordingStarted;
    private bool recordingFinalized;

    public WinAppSandbox(string runId, string controlRoot, string runRoot, string winappPath, Action saveJournal, TestContext context)
    {
        instanceId = Guid.Parse(runId);
        if (instanceId == Guid.Empty)
        {
            throw new WinAppSandboxException("run_identity_invalid");
        }

        this.runId = runId;
        this.controlRoot = Path.GetFullPath(controlRoot);
        this.runRoot = Path.GetFullPath(runRoot);
        this.winappPath = Path.GetFullPath(winappPath);
        this.saveJournal = saveJournal;
        this.context = context;
        wsbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wsb.exe");
        targetStateRoot = Path.Combine(this.controlRoot, "winapp-target");
        recordingPath = Path.Combine(this.runRoot, "recordings", "sandbox-guest.mp4");
        using var binary = File.OpenRead(this.winappPath);
        winappSha256 = Convert.ToHexString(SHA256.HashData(binary));
    }

    public string Backend => "WinApp";

    // Modern WindowsApps clients must never enter the legacy System32 PID-chain recovery.
    public IReadOnlyList<ProcessIdentity> Processes => Array.Empty<ProcessIdentity>();

    public long ViewerHwnd { get; private set; }

    public bool GuestAcknowledged { get; private set; }

    public JsonObject RecoveryState => (JsonObject)JsonSerializer.SerializeToNode(new
    {
        Backend,
        InstanceId = runId,
        CreationAttempted = creationAttempted,
        CreationConfirmed = creationConfirmed,
        OwnershipUnconfirmed = ownershipUnconfirmed,
        WinAppPath = winappPath,
        WinAppSha256 = winappSha256,
        WsbPath = wsbPath,
        TargetStateRoot = targetStateRoot,
        CommandProcesses = commandProcesses,
        Stopped = stopped,
    })!;

    public void Start(string configurationPath, string productArchive, string payloadRoot, string toolsRoot, EndpointChannel guest)
    {
        if (started || stopping)
        {
            throw new WinAppSandboxException("session_already_started");
        }

        started = true;
        this.guest = guest;
        try
        {
            ReadDeadlines(guest);
            AssertPrerequisites();
            var configuration = SandboxConfiguration.Create(productArchive, payloadRoot, toolsRoot, guest, legacyBootstrap: false);
            configuration.Save(configurationPath);
            stagedPayload = WinAppSandboxPayload.Create(
                productArchive, payloadRoot, toolsRoot, Path.Combine(controlRoot, "winapp-bootstrap-input"));
            WinAppSandboxProtocol.RequireEmptyInventory(Inventory(Budget(InventoryTimeout, bootstrap: true)));

            // Persist the trusted run-derived GUID before invoking a provider that
            // can create the instance even when it times out or returns an error.
            creationAttempted = true;
            saveJournal();
            var result = Run(wsbPath, ["start", "--id", instanceId.ToString("D"), "--config", configuration.ToString(SaveOptions.DisableFormatting), "--raw"],
                Budget(TimeSpan.FromMinutes(5), bootstrap: true));
            WinAppSandboxProtocol.RequireStartResult(result, instanceId);
            WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(Budget(InventoryTimeout, bootstrap: true)), instanceId);
            creationConfirmed = true;
            saveJournal();
            ConnectOwnedClient();

            // The first target command also installs the preview agent. Keep it
            // within the original endpoint deadline, not a shorter transfer cap.
            // A single transfer avoids repeating target preparation for each part
            // of the same immutable bootstrap input.
            Target(["push", "sandbox", stagedPayload, "MwbBootstrap", "--json"], TimeSpan.FromMinutes(15), bootstrap: true, firstBootstrap: true);
            Directory.Delete(stagedPayload, recursive: true);

            // No endpoint consumes these channels during SDK bootstrap. Keep the
            // actively publishing lease directory out of the cold guest until then.
            foreach (var mapping in SandboxConfiguration.EndpointMappings(guest))
            {
                ValidateOwnership(bootstrap: true);
                var arguments = new List<string>
                {
                    "share", "--id", instanceId.ToString("D"), "--host-path", mapping.HostPath,
                    "--sandbox-path", mapping.GuestPath, "--raw",
                };
                if (!mapping.ReadOnly)
                {
                    arguments.Add("--allow-write");
                }

                Run(wsbPath, arguments.ToArray(), Budget(TimeSpan.FromSeconds(30), bootstrap: true));
                ValidateOwnership(bootstrap: true);
            }

            ValidateOwnership(bootstrap: true);
            worker = StartAttached(
            [
                "target", "exec", "sandbox", "--", "powershell.exe", "-NoProfile", "-NonInteractive",
                "-ExecutionPolicy", "Bypass", "-STA", "-WindowStyle", "Hidden", "-File",
                @"C:\WinApp\work\MwbBootstrap\Payload\EndpointWorker.ps1", "-InputRoot", @"C:\MwbInput",
                "-OutputRoot", @"C:\MwbOutput", "-ProductRoot", @"C:\MwbProduct",
                "-WinApp", @"C:\WinApp\work\MwbBootstrap\Tools\winapp.exe", "-ProductArchive", @"C:\WinApp\work\MwbBootstrap\Runtime\product.zip",
            ]);
            RequireLiveWorker();
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            var errors = new List<Exception>(SafeFailures(error));
            try
            {
                Stop();
            }
            catch (AggregateException cleanup)
            {
                errors.AddRange(cleanup.InnerExceptions);
            }

            throw new AggregateException("Modern Sandbox startup failed; no backend or local fallback was attempted.", errors);
        }
    }

    public void Discover()
    {
        RequireLiveWorker();
        if (recorder is not null && recorder.HasExited)
        {
            throw recorder.Failure("recording_exited_early");
        }

        if (DateTime.UtcNow - lastDiscoveryUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        ValidateOwnership(bootstrap: !GuestAcknowledged);
        lastDiscoveryUtc = DateTime.UtcNow;
        RequireLiveWorker();
    }

    public void AcknowledgeGuest()
    {
        if (GuestAcknowledged || guest is null || !guest.Ready ||
            RunFiles.Read(Path.Combine(guest.OutputRoot, "ready.json"))["RunId"]?.GetValue<string>() != runId)
        {
            throw new WinAppSandboxException("guest_ready_identity_mismatch");
        }

        RequireLiveWorker();
        ValidateOwnership(bootstrap: true);
        // Correlated worker readiness has met the bootstrap deadline. Media
        // inspection remains bounded separately by the run's hard deadline.
        ReadSnapshot(bootstrap: false);
        GuestAcknowledged = true;
        saveJournal();
        StartRecording();
    }

    public void CaptureEvidence()
    {
        if (!GuestAcknowledged)
        {
            return;
        }

        RequireLiveWorker();
        // Assertions are finished. Finalize the video before another guest-wide
        // capture/inspection competes with its active capture channel.
        var recordingErrors = new List<Exception>();
        FinishRecording(recordingErrors);
        if (recordingErrors.Count != 0)
        {
            throw new AggregateException("Guest recording did not finalize before evidence capture.", recordingErrors);
        }

        var snapshot = ReadSnapshot(bootstrap: false);
        var snapshotPath = Path.Combine(runRoot, "winapp-sandbox-snapshot.json");
        snapshot["InstanceId"] = runId;
        RunFiles.Write(snapshotPath, snapshot);
        var privateScreenshot = Path.Combine(controlRoot, "sandbox-evidence.png");
        Target(["screenshot", "sandbox", "--output", privateScreenshot, "--json"], TimeSpan.FromSeconds(90));
        if (!HasOutput(privateScreenshot))
        {
            throw new WinAppSandboxException("screenshot_output_missing");
        }

        var screenshot = Path.Combine(runRoot, "winapp-sandbox.png");
        File.Move(privateScreenshot, screenshot, overwrite: false);
    }

    public void Stop()
    {
        if (stopped)
        {
            return;
        }

        stopping = true;
        var errors = new List<Exception>();
        Cleanup(() => FinishRecording(errors), errors);
        if (worker is not null)
        {
            Cleanup(() =>
            {
                if (!worker.WaitForExit(TimeSpan.FromSeconds(15)))
                {
                    errors.Add(new WinAppSandboxException("worker_forced_stop"));
                    worker.Terminate();
                }
                else
                {
                    worker.Complete(TimeSpan.FromSeconds(1)).RequireSuccess();
                }
            }, errors);
            Cleanup(worker.Dispose, errors);
            if (worker.HasExited)
            {
                worker = null;
            }
        }

        var absent = !creationAttempted;
        if (creationAttempted)
        {
            Cleanup(() =>
            {
                var inventory = WinAppSandboxProtocol.ReadInventory(Inventory(InventoryTimeout));
                if (inventory.Any(id => id != instanceId))
                {
                    ownershipUnconfirmed = true;
                }
                if (inventory.Contains(instanceId) || !creationConfirmed)
                {
                    // Even partial creation is fenced by the original GUID. Never
                    // use winapp target cleanup, which could act on an adopted VM.
                    Run(wsbPath, ["stop", "--id", instanceId.ToString("D"), "--raw"], TimeSpan.FromSeconds(45));
                }

                var timer = Stopwatch.StartNew();
                do
                {
                    var remaining = WinAppSandboxProtocol.ReadInventory(Inventory(InventoryTimeout - timer.Elapsed));
                    if (remaining.Any(id => id != instanceId))
                    {
                        ownershipUnconfirmed = true;
                    }

                    if (!remaining.Contains(instanceId))
                    {
                        absent = true;
                        break;
                    }

                    Thread.Sleep(250);
                }
                while (timer.Elapsed < TimeSpan.FromSeconds(30));

                if (!absent)
                {
                    throw new WinAppSandboxException("sandbox_stop_unconfirmed");
                }

                if (ownershipUnconfirmed)
                {
                    throw new WinAppSandboxException("sandbox_ownership_unconfirmed");
                }
            }, errors);
        }

        if (stagedPayload is not null && Directory.Exists(stagedPayload))
        {
            Cleanup(() =>
            {
                _ = WinAppSandboxPayload.PlainFiles(stagedPayload).ToArray();
                Directory.Delete(stagedPayload, recursive: true);
            }, errors);
        }

        if (absent && client is not null)
        {
            Cleanup(() =>
            {
                if (!client.HasExited)
                {
                    client.Kill();
                    if (!client.WaitForExit(15000))
                    {
                        throw new WinAppSandboxException("client_stop_timeout");
                    }
                }

                client.Dispose();
                client = null;
            }, errors);
        }

        if (creationAttempted && absent && !ownershipUnconfirmed && ownsTargetStateRoot && worker is null && recorder is null && client is null && Directory.Exists(targetStateRoot))
        {
            Cleanup(() =>
            {
                // Keep bootstrap secrets available for recovery until the exact
                // provider instance is absent and both attached clients have exited.
                // Pre-existing nonempty target state is never ours to delete.
                _ = WinAppSandboxPayload.PlainFiles(targetStateRoot).ToArray();
                Directory.Delete(targetStateRoot, recursive: true);
            }, errors);
        }

        stopped = absent && errors.Count == 0;
        Cleanup(saveJournal, errors);
        if (errors.Count != 0)
        {
            stopped = false;
            throw new AggregateException("Modern Sandbox cleanup requires recovery.", errors);
        }
    }

    private void AssertPrerequisites()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            throw new WinAppSandboxException("sandbox_unsupported");
        }

        var packages = new PackageManager().FindPackagesForUser(string.Empty, SandboxPackageFamily)
            .Where(package => package.Id.FamilyName == SandboxPackageFamily).ToArray();
        if (packages.Length != 1)
        {
            throw new WinAppSandboxException(WinAppSandboxPrerequisite.UserPackageRegistration);
        }

        if (!File.Exists(wsbPath) || (File.GetAttributes(wsbPath) & FileAttributes.ReparsePoint) == 0)
        {
            throw new WinAppSandboxException(WinAppSandboxPrerequisite.UserExecutionAlias);
        }

        trustedWsbExecutablePath = Path.Combine(packages[0].InstalledLocation.Path, "wsb.exe");
        if (!File.Exists(trustedWsbExecutablePath))
        {
            throw new WinAppSandboxException(WinAppSandboxPrerequisite.PackageExecutable);
        }

        WinAppSandboxPayload.RequirePlainPath(controlRoot);
        WinAppSandboxPayload.RequirePlainPath(winappPath);
        WinAppSandboxPayload.RequirePlainPath(targetStateRoot);
        if (targetStateRoot.StartsWith(runRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
        {
            throw new WinAppSandboxException("target_state_must_remain_private");
        }

        if (Directory.Exists(targetStateRoot) && Directory.EnumerateFileSystemEntries(targetStateRoot).Any())
        {
            throw new WinAppSandboxException("target_state_preexisting");
        }

        Directory.CreateDirectory(targetStateRoot);
        ownsTargetStateRoot = true;
        using (var stream = File.OpenRead(winappPath))
        {
            if (winappSha256 != Convert.ToHexString(SHA256.HashData(stream)))
            {
                throw new WinAppSandboxException("preview_binary_changed");
            }
        }

        WinAppSandboxProtocol.RequireCapabilities(Run(winappPath, ["--cli-schema"], Budget(TimeSpan.FromSeconds(30), bootstrap: true)));
        string version;
        try
        {
            version = Run(wsbPath, ["--version"], Budget(TimeSpan.FromSeconds(15), bootstrap: true));
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            throw new AggregateException(
                new Exception[] { new WinAppSandboxException(WinAppSandboxPrerequisite.ProviderVersion) }.Concat(SafeFailures(error)));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new WinAppSandboxException(WinAppSandboxPrerequisite.ProviderVersion);
        }
    }

    private void ReadDeadlines(EndpointChannel channel)
    {
        var bootstrap = RunFiles.Read(Path.Combine(channel.InputRoot, "bootstrap.json"));
        if (bootstrap["RunId"]?.GetValue<string>() != runId)
        {
            throw new WinAppSandboxException("bootstrap_identity_mismatch");
        }

        if (bootstrap["HardDeadlineUtc"] is not JsonValue hardDeadline || !hardDeadline.TryGetValue<DateTime>(out var hard) ||
            bootstrap["BootstrapDeadlineUtc"] is not JsonValue bootstrapDeadline || !bootstrapDeadline.TryGetValue<DateTime>(out var initial))
        {
            throw new WinAppSandboxException("bootstrap_deadline_invalid");
        }

        hardDeadlineUtc = hard.ToUniversalTime();
        bootstrapDeadlineUtc = initial.ToUniversalTime();
        if (bootstrapDeadlineUtc > DateTime.UtcNow.AddMinutes(15))
        {
            throw new WinAppSandboxException("bootstrap_deadline_invalid");
        }
    }

    private void ConnectOwnedClient()
    {
        ValidateOwnership(bootstrap: true, requireState: false);
        var deadline = DateTime.UtcNow + Budget(TimeSpan.FromMinutes(3), bootstrap: true);
        client = WinAppSandboxCommand.StartClient(
            wsbPath, ["connect", "--id", instanceId.ToString("D"), "--raw"], controlRoot);
        if (!client.WaitForExit((int)Budget(TimeSpan.FromSeconds(2), bootstrap: true).TotalMilliseconds))
        {
            try
            {
                var identity = ProcessIdentity.Capture(client.Id);
                if (identity.ParentId != Environment.ProcessId || identity.StartTimeUtc != client.StartTime.ToUniversalTime() ||
                    !string.Equals(identity.Path, trustedWsbExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new WinAppSandboxException("client_identity_mismatch");
                }

                commandProcesses.Add(identity);
                saveJournal();
            }
            catch (Exception error) when (error is not WinAppSandboxException &&
                (error is Win32Exception or InvalidOperationException) && client.HasExited && client.ExitCode == 0)
            {
                // Some client versions hand off to the window and exit immediately.
            }
        }

        while (DateTime.UtcNow < deadline)
        {
            if (client is not null && client.HasExited)
            {
                var exitCode = client.ExitCode;
                client.Dispose();
                client = null;
                if (exitCode != 0)
                {
                    throw new WinAppSandboxException("client_connect_failed", exitCode);
                }
            }

            try
            {
                var remaining = deadline - DateTime.UtcNow;
                var timeout = remaining < TimeSpan.FromSeconds(15) ? remaining : TimeSpan.FromSeconds(15);
                var result = Run(wsbPath,
                    ["exec", "--id", instanceId.ToString("D"), "--command", "cmd.exe /c exit 0", "--run-as", "ExistingLogin", "--raw"],
                    Budget(timeout, bootstrap: true));
                WinAppSandboxProtocol.RequireGuestCommandSuccess(result);
                ValidateOwnership(bootstrap: true, requireState: false);
                return;
            }
            catch (WinAppSandboxException error) when (error.ExitCode == WinAppSandboxProtocol.NoSuchLogonSession || error.Code == "command_timeout")
            {
                // The no-op is safe to repeat while this newly connected guest logs on.
            }

            Thread.Sleep(250);
        }

        throw new WinAppSandboxException("guest_login_timeout");
    }

    private TimeSpan Budget(TimeSpan maximum, bool bootstrap = false)
    {
        var deadline = bootstrap && bootstrapDeadlineUtc < hardDeadlineUtc ? bootstrapDeadlineUtc : hardDeadlineUtc;
        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new WinAppSandboxException("run_deadline_expired");
        }

        return remaining < maximum ? remaining : maximum;
    }

    private string Inventory(TimeSpan timeout) => Run(wsbPath, ["list", "--raw"], timeout);

    private string Run(string executable, string[] arguments, TimeSpan timeout)
    {
        var operation = arguments[0] == "target" ? arguments[1] : arguments[0];
        var trace = new JsonObject
        {
            ["Executable"] = Path.GetFileName(executable),
            ["Operation"] = operation,
            ["StartedUtc"] = DateTime.UtcNow,
            ["TimeoutSeconds"] = timeout.TotalSeconds,
            ["Status"] = "Running",
        };
        commandLog.Add(trace);
        RunFiles.Write(Path.Combine(runRoot, "winapp-commands.json"), commandLog);
        var timer = Stopwatch.StartNew();
        try
        {
            using var command = WinAppSandboxCommand.Start(executable, arguments, targetStateRoot, controlRoot);
            var result = command.CompleteAndDispose(timeout);
            trace["ExitCode"] = result.ExitCode;
            var output = result.RequireSuccess();
            trace["Status"] = "Completed";
            return output;
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            var codes = string.Join(", ", SafeFailures(error).Select(item => item.Code));
            trace["Status"] = "Failed";
            trace["ErrorCodes"] = codes;
            context.WriteLine($"Modern Sandbox {Path.GetFileName(executable)} {operation}: {codes}.");
            throw;
        }
        finally
        {
            trace["CompletedUtc"] = DateTime.UtcNow;
            trace["ElapsedSeconds"] = timer.Elapsed.TotalSeconds;
            RunFiles.Write(Path.Combine(runRoot, "winapp-commands.json"), commandLog);
        }
    }

    private string Target(string[] arguments, TimeSpan timeout, bool bootstrap = false, bool firstBootstrap = false)
    {
        if (stopping || !creationConfirmed)
        {
            throw new WinAppSandboxException("target_not_owned");
        }

        ValidateOwnership(bootstrap, requireState: !firstBootstrap);
        var result = Run(winappPath, ["target", .. arguments], Budget(timeout, bootstrap));
        ValidateOwnership(bootstrap);
        return result;
    }

    private void ValidateOwnership(bool bootstrap, bool requireState = true)
    {
        WinAppSandboxProtocol.RequireExclusiveInstance(
            () => Inventory(Budget(InventoryTimeout, bootstrap)),
            instanceId,
            () => ownershipUnconfirmed = true);
        try
        {
            if (requireState)
            {
                var stateFiles = WinAppSandboxPayload.PlainFiles(targetStateRoot).Where(path => Path.GetFileName(path) == "target-state.json").Take(2).ToArray();
                if (stateFiles.Any(path => new FileInfo(path).Length > 64 * 1024))
                {
                    throw new WinAppSandboxException("target_state_invalid");
                }

                epoch = WinAppSandboxProtocol.RequireAdoptedState(stateFiles.Select(ReadPrivateState).ToArray(), instanceId, epoch);
            }
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            ownershipUnconfirmed = true;
            throw;
        }
    }

    private JsonObject ReadSnapshot(bool bootstrap)
    {
        // The preview's reconnect alone permits sixty seconds, before its
        // read-only guest-window query. Do not cut it off after twenty.
        var json = Target(["snapshot", "sandbox", "--json"], TimeSpan.FromSeconds(90), bootstrap);
        var snapshot = WinAppSandboxProtocol.ReadSafeSnapshot(json, epoch!);
        ViewerHwnd = snapshot["ViewerHwnd"]!.GetValue<long>();
        return snapshot;
    }

    private WinAppSandboxCommand StartAttached(string[] arguments)
    {
        var command = WinAppSandboxCommand.Start(winappPath, arguments, targetStateRoot, controlRoot);
        try
        {
            if (command.HasExited)
            {
                throw command.Failure("attached_command_exited");
            }

            var identity = ProcessIdentity.Capture(command.ProcessId);
            if (command.HasExited || identity.ParentId != Environment.ProcessId || identity.StartTimeUtc != command.StartTimeUtc ||
                !string.Equals(identity.Path, winappPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new WinAppSandboxException("command_identity_mismatch");
            }

            commandProcesses.Add(identity);
            saveJournal();
            return command;
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            command.Dispose();
            throw Sanitize(error);
        }
    }

    private void RequireLiveWorker()
    {
        if (stopping || worker is null)
        {
            throw new WinAppSandboxException("worker_not_attached");
        }

        if (worker.HasExited)
        {
            throw worker.Failure("worker_exited");
        }
    }

    private void StartRecording()
    {
        ValidateOwnership(bootstrap: false);
        Directory.CreateDirectory(Path.GetDirectoryName(recordingPath)!);
        if (File.Exists(recordingPath))
        {
            throw new WinAppSandboxException("recording_output_preexisting");
        }

        recordingStartedUtc = DateTime.UtcNow;
        recorder = StartAttached(["target", "record", "sandbox", "--fps", "5", "--max-edge", "960", "--output", recordingPath, "--json"]);
        // Ownership is checked before launch and after readiness. Provider inventory
        // polling here can block delivery of an already-published readiness event.
        // Allow the preview's reconnect and capture initialization, not just capture.
        recorder.WaitForRecordingStart(Budget(TimeSpan.FromSeconds(90)), RequireLiveWorker);

        ValidateOwnership(bootstrap: false);
        RequireLiveWorker();
        if (recorder.HasExited)
        {
            throw recorder.Failure("recording_exited_early");
        }

        recordingStarted = true;
    }

    private void FinishRecording(List<Exception> errors)
    {
        if (recordingStartedUtc is null || recordingFinalized)
        {
            return;
        }

        var recordingErrors = new List<Exception>();
        int? exitCode = null;
        if (recorder is not null)
        {
            Cleanup(recorder.RequestRecordingStop, recordingErrors);
            Cleanup(() =>
            {
                var result = recorder.Complete(TimeSpan.FromSeconds(90));
                exitCode = result.ExitCode;
                result.RequireSuccess();
            }, recordingErrors);
            Cleanup(recorder.Dispose, recordingErrors);
            if (recorder.HasExited)
            {
                recorder = null;
            }
        }

        if (!recordingStarted || !HasOutput(recordingPath))
        {
            recordingErrors.Add(new WinAppSandboxException("recording_output_unavailable"));
        }

        Cleanup(() =>
        {
            var available = HasOutput(recordingPath);
            var manifest = Path.Combine(runRoot, "recordings", "sandbox-guest.json");
            RunFiles.Write(manifest, new
            {
                Backend,
                InstanceId = runId,
                StartedUtc = recordingStartedUtc,
                StoppedUtc = DateTime.UtcNow,
                Started = recordingStarted,
                Completed = recordingErrors.Count == 0 && exitCode == 0 && available,
                Available = available,
                ExitCode = exitCode,
                FrameRate = 5,
                MaxEdge = 960,
                File = available ? Path.GetFileName(recordingPath) : null,
                Errors = recordingErrors.Select(error => Sanitize(error).Code).ToArray(),
            });
            context.AddResultFile(manifest);
            if (available)
            {
                context.AddResultFile(recordingPath);
            }

            if (recordingErrors.Count != 0)
            {
                context.WriteLine("Guest recording did not finalize cleanly; any nonempty MP4 was retained. See the safe Sandbox recording manifest.");
            }
        }, recordingErrors);
        recordingFinalized = recorder is null;
        errors.AddRange(recordingErrors);
    }

    private static string ReadPrivateState(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool HasOutput(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private static bool IsExpectedFailure(Exception error) =>
        error is InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception or COMException or JsonException or ArgumentException or TimeoutException ||
        (error is AggregateException aggregate && aggregate.InnerExceptions.All(IsExpectedFailure));

    private static IEnumerable<WinAppSandboxException> SafeFailures(Exception error) =>
        error is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.Select(Sanitize)
            : [Sanitize(error)];

    private static WinAppSandboxException Sanitize(Exception error) =>
        error as WinAppSandboxException ?? new WinAppSandboxException("sandbox_infrastructure_failed");

    private static void Cleanup(Action action, List<Exception> errors)
    {
        try
        {
            action();
        }
        catch (Exception error) when (IsExpectedFailure(error))
        {
            errors.AddRange(SafeFailures(error));
        }
    }
}
