// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class LegacySandbox
{
    private readonly List<ProcessIdentity> owned = [];
    private readonly Action saveJournal;
    private readonly Action<long> captureViewer;
    private readonly int sessionId = Process.GetCurrentProcess().SessionId;
    private readonly string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private long viewerHwnd;
    private bool stopping;

    public LegacySandbox(Action saveJournal, Action<long> captureViewer)
    {
        this.saveJournal = saveJournal;
        this.captureViewer = captureViewer;
    }

    public IReadOnlyList<ProcessIdentity> Processes => owned;

    public long ViewerHwnd => viewerHwnd;

    public bool GuestAcknowledged { get; private set; }

    public static void AssertNoneRunning()
    {
        if (AnySandboxProcessRunning())
        {
            throw new InvalidOperationException("A pre-existing Windows Sandbox was found; refusing to commandeer it.");
        }
    }

    private static bool AnySandboxProcessRunning()
    {
        bool found = false;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                }
            }
        }

        return found;
    }

    public void Start(string configurationPath, string productArchive, string payloadRoot, string toolsRoot, EndpointChannel guest)
    {
        AssertNoneRunning();
        static XElement Mapping(string source, string target, bool readOnly) =>
            new("MappedFolder", new XElement("HostFolder", source), new XElement("SandboxFolder", target), new XElement("ReadOnly", readOnly));

        var document = new XDocument(
            new XElement(
                "Configuration",
                // Enabling vGPU (tested: run localvm-20260914-073258, RunId
                // 34fdda19-ac0f-43f5-9b5e-e7158e2331da) made no difference to the WinUI3
                // startup failure below, so keep the more conservative, isolation-preserving
                // default rather than leave an unproven change in place.
                new XElement("VGpu", "Disable"),
                new XElement("MemoryInMB", 4096),
                new XElement("Networking", "Enable"),
                new XElement("ClipboardRedirection", "Disable"),
                new XElement("AudioInput", "Disable"),
                new XElement("VideoInput", "Disable"),
                new XElement("PrinterRedirection", "Disable"),
                new XElement("MappedFolders",
                    Mapping(Path.GetDirectoryName(productArchive)!, @"C:\MwbArchive", true),
                    Mapping(payloadRoot, @"C:\MwbPayload", true),
                    Mapping(toolsRoot, @"C:\MwbTools", true),
                    Mapping(guest.InputRoot, @"C:\MwbInput", true),
                    Mapping(guest.OutputRoot, @"C:\MwbOutput", false)),
                new XElement("LogonCommand", new XElement("Command",
                    @"powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File C:\MwbPayload\EndpointWorker.ps1 -InputRoot C:\MwbInput -OutputRoot C:\MwbOutput -ProductRoot C:\MwbProduct -WinApp C:\MwbTools\winapp.exe -ProductArchive " +
                    "\"C:\\MwbArchive\\" + Path.GetFileName(productArchive) + "\""))));
        document.Save(configurationPath);
        var start = new ProcessStartInfo(Path.Combine(systemRoot, "System32", "WindowsSandbox.exe"))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(configurationPath)!,
        };
        start.ArgumentList.Add(configurationPath);
        using var launcher = Process.Start(start) ?? throw new InvalidOperationException("Sandbox launcher did not start.");
        owned.Add(ProcessIdentity.Capture(launcher.Id));
        saveJournal();
    }

    public void Discover()
    {
        // Windows versions use different client/remote-session executables. A name is only a
        // candidate filter; path, session, birth time and the recorded parent chain prove ownership.
        bool added;
        do
        {
            added = false;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (!process.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase) ||
                        process.SessionId != sessionId ||
                        owned.Any(record => record.Id == process.Id))
                    {
                        continue;
                    }

                    ProcessIdentity record;
                    try
                    {
                        record = ProcessIdentity.Capture(process.Id);
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    var parent = owned.SingleOrDefault(item => item.Id == record.ParentId);
                    if (parent is null || record.SessionId != sessionId || record.StartTimeUtc < parent.StartTimeUtc ||
                        !record.Path.StartsWith(systemRoot + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var currentParent = TryGetProcess(parent.Id);
                    if (currentParent is not null && !parent.IsCurrent())
                    {
                        continue;
                    }

                    owned.Add(record);
                    added = true;
                }
            }
        }
        while (added);
        var windows = WindowControl.EnumerateProcessWindows(owned.Where(item => item.IsCurrent()).Select(item => item.Id).ToArray());
        var viewer = windows.Where(window => window.IsVisible && window.Width > 400 && window.Height > 300)
            .OrderByDescending(window => window.Width * window.Height).FirstOrDefault();
        if (viewer.Hwnd != IntPtr.Zero)
        {
            viewerHwnd = viewer.Hwnd.ToInt64();
        }

        saveJournal();
        if (!GuestAcknowledged && !stopping)
        {
            foreach (var dialog in windows.Where(window => window.IsVisible && window.ClassName == "#32770"))
            {
                var process = owned.Single(record => record.Id == dialog.ProcessId);
                var error = SandboxStartupError.Read(
                    Path.GetFileName(process.Path),
                    dialog.ClassName,
                    () => NativeSupport.DialogStaticText(dialog.Hwnd, dialog.ProcessId));
                if (error is not null)
                {
                    throw new InvalidOperationException(error);
                }
            }
        }
    }

    public void AcknowledgeGuest()
    {
        Discover();
        if (viewerHwnd == 0)
        {
            throw new InvalidOperationException("Guest acknowledged this run, but no owned legacy viewer HWND was discovered.");
        }

        GuestAcknowledged = true;
        saveJournal();

        // Encoding must not block the readiness polling path. Desktop capture
        // already covers startup while this separate viewer capture initializes.
        captureViewer(viewerHwnd);
    }

    public void Stop()
    {
        stopping = true;
        if (owned.Count == 0)
        {
            return;
        }

        var errors = new List<Exception>();
        try
        {
            Discover();
            if (viewerHwnd != 0)
            {
                var owner = WindowControl.EnumerateProcessWindows(owned.Where(item => item.IsCurrent()).Select(item => item.Id).ToArray())
                    .SingleOrDefault(window => window.Hwnd.ToInt64() == viewerHwnd);
                if (owner.Hwnd != IntPtr.Zero)
                {
                    NativeSupport.PostMessage(owner.Hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }

                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(20) && owned.Any(item => item.IsCurrent()))
                {
                    if (owner.Hwnd != IntPtr.Zero)
                    {
                        NativeSupport.ConfirmSandboxClose(owner.Hwnd, owner.ProcessId);
                    }

                    Thread.Sleep(250);
                }
            }
        }
        catch (Exception error)
        {
            errors.Add(error);
        }

        // No service/Hyper-V-wide stop. Only records proved descendants of our original launcher.
        foreach (var process in owned.AsEnumerable().Reverse())
        {
            try
            {
                process.Stop();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        try
        {
            RunFiles.Wait(
                () => !AnySandboxProcessRunning(),
                TimeSpan.FromSeconds(15),
                "Sandbox processes remain after owned-viewer cleanup; privileged controller recovery is required.");
        }
        catch (Exception error)
        {
            errors.Add(error);
        }

        saveJournal();
        if (errors.Count > 0)
        {
            throw new AggregateException("Legacy Sandbox cleanup was incomplete.", errors);
        }
    }

    private static Process? TryGetProcess(int id)
    {
        try
        {
            return Process.GetProcessById(id);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
