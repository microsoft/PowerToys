// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

#nullable enable

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Service-initialization block (Design-v6-Final.md §11 "Lazy per-user install").
///
/// The per-machine MSI registers PTSettingsSvc eagerly at install time.  A
/// per-user install ships the service payload unregistered; this block performs
/// the one-time elevation that registers the machine-wide service and hardens
/// the current user's protected store the first time protection is actually
/// needed.  It is deliberately self-contained so the same logic can be invoked
/// from any trigger point (editor open, first save, workspace launch, an
/// explicit Settings toggle) — see <see cref="SettingsBootstrapper"/>.
///
/// The elevation step is injectable (<see cref="ElevationRunner"/>) so callers
/// and tests can substitute the UAC prompt with a direct run.
/// </summary>
public static class ServiceProvisioner
{
    /// <summary>Result of an attempt to provision the service for the current user.</summary>
    public enum Outcome
    {
        /// <summary>The service was already reachable; nothing to do.</summary>
        ServiceAvailable,

        /// <summary>Elevation ran and the service is now reachable.</summary>
        Provisioned,

        /// <summary>Elevation ran but the service still isn't reachable.</summary>
        AttemptedNotConfirmed,

        /// <summary>A prior attempt was already made; not re-prompting (unless forced).</summary>
        AlreadyAttempted,

        /// <summary>The user declined the elevation (UAC cancelled).</summary>
        UserDeclined,

        /// <summary>The service payload (exe / script) was not found in the install.</summary>
        PayloadMissing,

        /// <summary>The elevation could not be launched at all.</summary>
        ElevationFailed,
    }

    /// <summary>Outcome of launching the elevated provisioning helper.</summary>
    public enum ElevationResult
    {
        /// <summary>The elevated helper ran to completion.</summary>
        Completed,

        /// <summary>The user cancelled the UAC prompt.</summary>
        Declined,

        /// <summary>The helper could not be launched.</summary>
        Failed,
    }

    /// <summary>
    /// Launches the elevated provisioning helper.  Implementations must block
    /// until the helper exits and report whether it completed, was declined, or
    /// failed to launch.  The default is <see cref="RunElevatedPowerShell"/>.
    /// </summary>
    public delegate ElevationResult ElevationRunner(string fileName, string arguments);

    /// <summary>True when the service answers (installed and running).</summary>
    public static bool IsServiceAvailable()
    {
        // Fast pre-check: if the named pipe doesn't exist, the service isn't
        // running, so skip PTSettingsClient.Ping() whose connect waits out a
        // multi-second timeout for a missing pipe.  This keeps the common
        // "no service yet" path (per-user, pre-provision) cheap (~ms) instead
        // of blocking the caller for the full connect timeout.
        if (!PipeExists())
        {
            return false;
        }

        return PTSettingsClient.Ping() != PTSettingsClient.Result.Unavailable;
    }

    private static bool PipeExists()
    {
        try
        {
            foreach (var pipe in Directory.EnumerateFiles(@"\\.\pipe\"))
            {
                if (string.Equals(Path.GetFileName(pipe), PTSettingsClient.PipeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // If enumeration fails for any reason, fall back to the (slower but
            // authoritative) connect probe rather than wrongly reporting absent.
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures the service is provisioned for the current user, performing the
    /// one-time elevation if needed.  Idempotent and sentinel-guarded so it is
    /// safe to call from multiple trigger points.
    /// </summary>
    public static Outcome EnsureProvisioned(ProvisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsServiceAvailable())
        {
            return Outcome.ServiceAvailable;
        }

        // Back off if we've already prompted this user, unless the caller forces
        // it (e.g. an explicit "enable protection" action in Settings).
        if (!options.Force && File.Exists(SettingsPaths.ProvisionAttemptSentinel()))
        {
            return Outcome.AlreadyAttempted;
        }

        var serviceMsix = options.ServiceMsixPath;
        if (string.IsNullOrEmpty(serviceMsix) || !File.Exists(serviceMsix))
        {
            // No package to install from (e.g. a no-admin xcopy deployment).
            // Don't write the sentinel: a later install that adds the payload
            // should still be allowed to try.
            return Outcome.PayloadMissing;
        }

        var userSid = string.IsNullOrEmpty(options.UserSid)
            ? WindowsIdentity.GetCurrent().User?.Value
            : options.UserSid;
        if (string.IsNullOrEmpty(userSid))
        {
            return Outcome.ElevationFailed;
        }

        // Record the attempt up front so a crash mid-elevation doesn't make us
        // re-prompt on the next trigger.
        TryWriteAttemptSentinel();

        var runner = options.ElevationRunner ?? RunElevatedPowerShell;
        var arguments = BuildInstallArguments(serviceMsix);

        var elevation = runner("powershell.exe", arguments);
        switch (elevation)
        {
            case ElevationResult.Declined:
                return Outcome.UserDeclined;

            case ElevationResult.Failed:
                return Outcome.ElevationFailed;

            case ElevationResult.Completed:
            default:
                return IsServiceAvailable() ? Outcome.Provisioned : Outcome.AttemptedNotConfirmed;
        }
    }

    /// <summary>
    /// Builds the elevated install command. Deploys the SIGNED service MSIX via
    /// <c>Add-AppxPackage</c> — an inline command (in our signed binary, NOT a
    /// user-writable script) whose only payload is the signed .msix; the OS
    /// verifies its signature on deploy, so this cannot run attacker code. The
    /// packaged windows.service extension auto-registers PTSettingsSvc; DACL and
    /// migration are then done by the LocalSystem service (Design §12.1) — no
    /// extra elevation. Replaces the retired user-writable Harden-PtSettings ps1.
    /// </summary>
    public static string BuildInstallArguments(string serviceMsix)
    {
        return "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "
             + "\"Add-AppxPackage -Path '" + serviceMsix + "' -ForceUpdateFromAnyVersion\"";
    }

    /// <summary>
    /// Default elevation runner: launches PowerShell elevated (UAC) and waits.
    /// Maps a cancelled UAC prompt to <see cref="ElevationResult.Declined"/>.
    /// </summary>
    public static ElevationResult RunElevatedPowerShell(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return ElevationResult.Failed;
            }

            proc.WaitForExit();
            return ElevationResult.Completed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED — the user dismissed the UAC prompt.
            return ElevationResult.Declined;
        }
        catch (Win32Exception)
        {
            return ElevationResult.Failed;
        }
        catch (InvalidOperationException)
        {
            return ElevationResult.Failed;
        }
    }

    private static void TryWriteAttemptSentinel()
    {
        try
        {
            var sentinel = SettingsPaths.ProvisionAttemptSentinel();
            var dir = Path.GetDirectoryName(sentinel);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(sentinel, DateTime.UtcNow.ToString("o"));
        }
        catch (IOException)
        {
            // Best-effort: a missing sentinel only means we may re-prompt once more.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
