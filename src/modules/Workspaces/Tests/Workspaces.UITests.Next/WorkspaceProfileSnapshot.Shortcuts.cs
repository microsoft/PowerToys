// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Workspaces.UITests
{
    internal sealed partial class WorkspaceProfileSnapshot
    {
        private const string SavedJournals = "user-shortcut-journals";
        private const string TestShortcuts = "test-shortcuts";
        private const string JournalExtension = ".links.json";
        private const string DesktopRecord = ".desktop";
        private const string InspectionCopy = ".inspect.lnk";
        private readonly string desktopDirectory;

        private enum LinkCleanup
        {
            Absent,
            Removed,
            Foreign,
        }

        private string IconDirectory => Path.Combine(workspaceDirectory, "WorkspacesIcons");

        // The guard keeps a delete-sharing handle to each original file. Windows can rename
        // those files, but cannot commit the capturing directory with an open file beneath it.
        private string SavedJournalDirectory => Path.Combine(recoveryDirectory, SavedJournals);

        private void CaptureShortcutJournals()
        {
            if (!RegularDirectoryExists(IconDirectory))
            {
                return;
            }

            // Use the same set of names that WorkspaceShortcutJournal.EnumerateIds can open.
            // Leave unrelated files in place. A redirected journal must stop isolation.
            var journals = Directory.GetFileSystemEntries(IconDirectory, "*" + JournalExtension)
                .Where(path => IsJournalName(Path.GetFileName(path))).ToArray();
            foreach (var journal in journals)
            {
                var attributes = File.GetAttributes(journal);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory)
                {
                    continue;
                }

                RequireRegularFileOrMissing(journal);
                Directory.CreateDirectory(SavedJournalDirectory);
                RequireRegularDirectory(SavedJournalDirectory);
                Rename(journal, Path.Combine(SavedJournalDirectory, Path.GetFileName(journal)));
            }
        }

        internal void TrackProjectShortcutFiles(string id)
        {
            if (!isolated || !TryProjectId(id, out var projectId))
            {
                throw new InvalidOperationException($"Cannot register Workspaces shortcut files for '{id}'.");
            }

            var owners = Path.Combine(original, TestShortcuts);
            var owner = Path.Combine(owners, id);
            var journalName = $"{projectId:B}{JournalExtension}";
            if (RegularDirectoryExists(owners) && RequireRegularFileOrMissing(owner))
            {
                return;
            }

            if ((RegularDirectoryExists(SavedJournalDirectory) && RequireRegularFileOrMissing(Path.Combine(SavedJournalDirectory, journalName))) ||
                (RegularDirectoryExists(IconDirectory) && (RequireRegularFileOrMissing(Path.Combine(IconDirectory, id + ".ico")) ||
                    RequireRegularFileOrMissing(Path.Combine(IconDirectory, journalName)))))
            {
                throw new IOException($"Shortcut files already exist for the test project '{id}'.");
            }

            Directory.CreateDirectory(owners);
            File.Open(owner, FileMode.CreateNew).Dispose();
        }

        internal void TrackDesktopShortcut(string name)
        {
            if (!isolated || !IsTestShortcutName(name))
            {
                throw new InvalidOperationException($"Cannot register Workspaces desktop shortcut '{name}'.");
            }

            var owners = Path.Combine(original, TestShortcuts);
            Directory.CreateDirectory(owners);
            RequireRegularDirectory(owners);
            RequireRegularDirectory(desktopDirectory);
            var marker = Path.Combine(owners, name + ".lnk");
            if (RequireRegularFileOrMissing(marker))
            {
                return;
            }

            if (RequireRegularFileOrMissing(Path.Combine(desktopDirectory, name + ".lnk")))
            {
                throw new IOException($"A desktop shortcut already exists for '{name}'.");
            }

            // Desktop can be redirected to another directory or volume between test runs.
            var location = Path.Combine(owners, DesktopRecord);
            if (!RequireRegularFileOrMissing(location))
            {
                if (!Path.IsPathFullyQualified(desktopDirectory))
                {
                    throw new IOException("The test desktop directory has no absolute path.");
                }

                File.WriteAllText(location, Path.TrimEndingDirectorySeparator(desktopDirectory));
            }
            else if (!string.Equals(File.ReadAllText(location), Path.TrimEndingDirectorySeparator(desktopDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The desktop location changed during the Workspaces UI test run.");
            }

            File.Open(marker, FileMode.CreateNew).Dispose();
        }

        private void RestoreShortcuts(string stage, string interruptedDirectory, bool interrupted)
        {
            var owners = Path.Combine(stage, TestShortcuts);
            if (RegularDirectoryExists(owners))
            {
                var interruptedCopy = Path.Combine(owners, InspectionCopy);
                if (RequireRegularFileOrMissing(interruptedCopy))
                {
                    File.Delete(interruptedCopy);
                }

                var location = Path.Combine(owners, DesktopRecord);
                var projects = new List<FileInfo>();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in new DirectoryInfo(owners).GetFileSystemInfos())
                {
                    if (entry is not FileInfo file || !RequireRegularFileOrMissing(entry.FullName))
                    {
                        throw new InvalidDataException($"Invalid shortcut recovery entry '{entry.FullName}'.");
                    }

                    if (file.Name == DesktopRecord)
                    {
                        continue;
                    }

                    if (file.Length == 0 && TryProjectId(file.Name, out _))
                    {
                        projects.Add(file);
                    }
                    else if (file.Length == 0 && file.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                        IsTestShortcutName(Path.GetFileNameWithoutExtension(file.Name)))
                    {
                        names.Add(Path.GetFileNameWithoutExtension(file.Name));
                    }
                    else
                    {
                        throw new InvalidDataException($"Unexpected shortcut registration '{file.FullName}'.");
                    }
                }

                var desktop = names.Count == 0 ? null : File.ReadAllText(location);
                if (desktop is not null && (!Path.IsPathFullyQualified(desktop) ||
                    !string.Equals(desktop, Path.TrimEndingDirectorySeparator(Path.GetFullPath(desktop)), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException($"Invalid desktop location in '{location}'.");
                }

                // Retire owned links before touching any icons: a replacement at an A name
                // can reference B's icon even though B's journal doesn't claim that name.
                var results = new Dictionary<string, LinkCleanup>(StringComparer.OrdinalIgnoreCase);
                foreach (var project in projects)
                {
                    var id = Guid.Parse(project.Name);
                    var journal = Path.Combine(IconDirectory, $"{id:B}{JournalExtension}");
                    if (!RegularDirectoryExists(IconDirectory) || !RequireRegularFileOrMissing(journal) || ReadJournalNames(journal, id) is not { } entries)
                    {
                        continue;
                    }

                    var result = LinkCleanup.Absent;
                    foreach (var name in entries.Where(names.Contains))
                    {
                        RequireRegularDirectory(desktop!);
                        var link = RemoveOwnedLink(Path.Combine(desktop!, name + ".lnk"), id, owners);
                        if (link != LinkCleanup.Absent)
                        {
                            result = link;
                        }
                    }

                    if (entries.All(names.Contains))
                    {
                        results.Add(project.Name, result);
                    }
                }

                // If a reserved name now contains another link, its icon may be referenced
                // by a relative path, alias or environment variable. Keep the available icons.
                bool keepIcons = desktop is not null && names.Any(name => RequireRegularFileOrMissing(Path.Combine(desktop, name + ".lnk")));
                foreach (var project in projects)
                {
                    if (results.TryGetValue(project.Name, out var result))
                    {
                        var journal = Path.Combine(IconDirectory, $"{Guid.Parse(project.Name):B}{JournalExtension}");
                        var icon = Path.Combine(IconDirectory, project.Name + ".ico");
                        if (!keepIcons && !RequireRegularFileOrMissing(journal + ".corrupt") && RequireRegularFileOrMissing(icon))
                        {
                            // Preserve ambiguous icon bytes when the desktop link already
                            // disappeared during an interrupted run.
                            string? archive = interrupted && result != LinkCleanup.Removed ? Path.Combine(interruptedDirectory, TestShortcuts) : null;
                            DisplaceCurrent(icon, archive);
                        }

                        File.Delete(journal);
                    }

                    project.Delete();
                }

                foreach (var name in names)
                {
                    File.Delete(Path.Combine(owners, name + ".lnk"));
                }

                File.Delete(location);
                Directory.Delete(owners);
            }

            var saved = SavedJournalDirectory;
            var previousLocation = Path.Combine(stage, SavedJournals);
            if (RegularDirectoryExists(previousLocation))
            {
                if (RegularDirectoryExists(saved))
                {
                    throw new InvalidDataException($"Conflicting Workspaces shortcut journal backups in '{stage}' and '{saved}'.");
                }

                saved = previousLocation;
            }

            if (!RegularDirectoryExists(saved))
            {
                return;
            }

            Directory.CreateDirectory(IconDirectory);
            RequireRegularDirectory(IconDirectory);
            foreach (var source in Directory.GetFileSystemEntries(saved))
            {
                var name = Path.GetFileName(source);
                if (!IsJournalName(name) || !RequireRegularFileOrMissing(source))
                {
                    throw new InvalidDataException($"Unexpected saved shortcut journal '{source}'.");
                }

                var path = Path.Combine(IconDirectory, name);
                DisplaceCurrent(path, Path.Combine(interruptedDirectory, SavedJournals));
                Rename(source, path);
            }

            Directory.Delete(saved);
        }

        private static LinkCleanup RemoveOwnedLink(string path, Guid id, string ownerDirectory)
        {
            if (!RequireRegularFileOrMissing(path))
            {
                return LinkCleanup.Absent;
            }

            const uint readAndDelete = 0x80010000; // GENERIC_READ | DELETE
            const uint openReparsePoint = 0x00200000;
            using var handle = CreateFileW(path, readAndDelete, FileShare.Read, IntPtr.Zero, FileMode.Open, openReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                if (error is 2 or 3)
                {
                    return LinkCleanup.Absent;
                }

                throw new IOException($"Could not inspect the registered shortcut '{path}'.", new Win32Exception(error));
            }

            // The Shell opens a second handle when loading a .lnk. Read the stable bytes
            // into a private copy, then retire the file through the original handle.
            RequireRegularFileOrMissing(path);
            using var source = new FileStream(handle, FileAccess.Read);
            const int maxShellLinkBytes = 4 * 1024 * 1024; // WorkspaceShortcutFile.ReadContents
            if (source.Length > maxShellLinkBytes)
            {
                return LinkCleanup.Foreign;
            }

            var copy = Path.Combine(ownerDirectory, InspectionCopy);
            string target;
            string arguments;
            var output = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (output)
                {
                    source.CopyTo(output);
                }

                (target, arguments) = ReadShortcutTargetAndArguments(copy);
            }
            catch (COMException error) when (error.HResult is unchecked((int)0x8007000B) or unchecked((int)0x80004005) or
                unchecked((int)0x8003001E) or unchecked((int)0x800300FB) or unchecked((int)0x80030109))
            {
                // Another writer can replace a registered name with a non-shortcut.
                return LinkCleanup.Foreign;
            }
            finally
            {
                File.Delete(copy);
            }

            var tokens = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (string.Equals(Path.GetFileName(target), "PowerToys.WorkspacesLauncher.exe", StringComparison.OrdinalIgnoreCase) &&
                tokens is [var token, "1"] && Guid.TryParseExact(token, "B", out var linkId) && linkId == id)
            {
                const int fileDispositionInfo = 4;
                byte deleteFile = 1;
                if (!SetFileInformationByHandle(handle, fileDispositionInfo, ref deleteFile, sizeof(byte)))
                {
                    throw new IOException($"Could not retire the registered shortcut '{path}'.", new Win32Exception(Marshal.GetLastWin32Error()));
                }

                return LinkCleanup.Removed;
            }

            return LinkCleanup.Foreign;
        }

        private static string[]? ReadJournalNames(string path, Guid id)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("ProjectId", out var project) || project.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(project.GetString(), out var owner) || owner != id ||
                    !root.TryGetProperty("PendingRemoval", out var removal) || removal.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !root.TryGetProperty("Names", out var names) || names.ValueKind != JsonValueKind.Array ||
                    names.EnumerateArray().Any(name => name.ValueKind != JsonValueKind.String))
                {
                    return null;
                }

                return names.EnumerateArray().Select(name => name.GetString()!).ToArray();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsJournalName(string name) =>
            name.Length == 38 + JournalExtension.Length && name.EndsWith(JournalExtension, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(name.AsSpan(0, 38), "B", out _);

        private static bool TryProjectId(string id, out Guid value) =>
            (Guid.TryParseExact(id, "B", out value) && id.Length == 38) || (Guid.TryParseExact(id, "D", out value) && id.Length == 36);

        private static bool IsTestShortcutName(string name)
        {
            const string prefix = "PTUITest-";
            return name.Length > prefix.Length + 33 && name.StartsWith(prefix, StringComparison.Ordinal) &&
                name[prefix.Length + 32] == '-' && Guid.TryParseExact(name.AsSpan(prefix.Length, 32), "N", out _) &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static bool RegularDirectoryExists(string path)
        {
            try
            {
                RequireRegularDirectory(path);
                return true;
            }
            catch (Exception error) when (error is DirectoryNotFoundException or FileNotFoundException)
            {
                return false;
            }
        }

        internal static (string Target, string Arguments) ReadShortcutTargetAndArguments(string path)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not registered.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic link = shell.CreateShortcut(path);
                try
                {
                    return ((string)link.TargetPath, (string)link.Arguments);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, FileShare share, IntPtr security, FileMode creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref byte info, uint size);
    }
}
