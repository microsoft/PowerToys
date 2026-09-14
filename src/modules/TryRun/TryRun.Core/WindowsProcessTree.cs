// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.TryRun.Core;

// Read-only process association for the visual marker. MXC owns containment and
// process termination; this tracker neither creates jobs nor changes permissions.
public sealed partial class WindowsProcessTree : IDisposable
{
    private const int MaximumProcesses = 256;
    private readonly Dictionary<uint, SafeProcessHandle> processes = [];
    private readonly uint rootId;

    public WindowsProcessTree(WindowsProcessIdentity root)
    {
        ArgumentNullException.ThrowIfNull(root);
        rootId = root.ProcessId;
        var handle = Open(rootId);
        if (TryGetTimes(handle, out var created, out _) && created == root.CreationTime && IsAlive(handle))
        {
            processes.Add(rootId, handle);
        }
        else
        {
            handle.Dispose();
        }
    }

    public bool IsRunning => Contains(rootId);

    public bool Contains(uint processId) => processes.TryGetValue(processId, out var handle) && IsAlive(handle);

    public void Refresh()
    {
        if (!IsRunning || processes.Count >= MaximumProcesses)
        {
            return;
        }

        using var snapshot = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
        if (snapshot.IsInvalid)
        {
            return;
        }

        var candidates = new List<(uint Id, uint Parent)>();
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (Process32FirstW(snapshot, ref entry))
        {
            do
            {
                if (entry.ProcessId != 0 && !processes.ContainsKey(entry.ProcessId))
                {
                    candidates.Add((entry.ProcessId, entry.ParentProcessId));
                }
            }
            while (candidates.Count < 16384 && Process32NextW(snapshot, ref entry));
        }

        // A snapshot need not list parents before children. Keep handles for the
        // session lifetime, and compare creation/exit times to reject PID reuse.
        bool added;
        do
        {
            added = false;
            foreach (var candidate in candidates)
            {
                if (processes.Count >= MaximumProcesses)
                {
                    return;
                }

                if (processes.ContainsKey(candidate.Id) || !processes.TryGetValue(candidate.Parent, out var parent) || !TryGetTimes(parent, out var parentCreated, out var parentExited))
                {
                    continue;
                }

                var child = Open(candidate.Id);
                if (TryGetTimes(child, out var created, out _) && IsAlive(child) && created >= parentCreated && (parentExited == 0 || created <= parentExited))
                {
                    processes.Add(candidate.Id, child);
                    added = true;
                }
                else
                {
                    child.Dispose();
                }
            }
        }
        while (added);
    }

    public void Dispose()
    {
        foreach (var process in processes.Values)
        {
            process.Dispose();
        }

        processes.Clear();
    }

    internal static SafeProcessHandle Open(uint processId) => OpenProcess(0x00101000, false, processId); // SYNCHRONIZE | QUERY_LIMITED_INFORMATION

    internal static bool IsAlive(SafeProcessHandle process) => !process.IsInvalid && !process.IsClosed && WaitForSingleObject(process, 0) == 258;

    internal static bool TryGetTimes(SafeProcessHandle process, out long created, out long exited)
    {
        created = 0;
        exited = 0;
        return !process.IsInvalid && !process.IsClosed && GetProcessTimes(process, out created, out exited, out _, out _);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;
        public fixed char Executable[260];
    }

    [LibraryImport("kernel32.dll")]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle handle, out long created, out long exited, out long kernel, out long user);

    [LibraryImport("kernel32.dll")]
    private static partial SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);
}
