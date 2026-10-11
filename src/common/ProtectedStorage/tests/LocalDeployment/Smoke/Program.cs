// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Principal;
using System.Text.Json;
using PowerToys.ProtectedStorage;
using WorkspacesCsharpLibrary.Data;

namespace ProtectedStorage.LocalSmoke;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            return 2;
        }

        try
        {
            if (!StorageIdentity.IsLocalTest || StorageIdentity.ProductName != "PowerToysProtectedStorageLocalTest")
            {
                throw new InvalidOperationException("This smoke test must use the isolated local-test client.");
            }

            object result = await RunAsync(args[0]);
            await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(result));
            return 0;
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new
            {
                ok = false,
                error = error.ToString(),
                nativeCode = (error as ProtectedStorageException)?.NativeCode,
            }));
            return 1;
        }
    }

    private static ProjectWrapper Project(string name) => new()
    {
        Id = Guid.NewGuid().ToString("B"),
        Name = name,
        CreationTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        MonitorConfiguration =
        [
            new()
            {
                Id = "LOCAL-TEST-MONITOR",
                InstanceId = "LOCAL-TEST-MONITOR",
                MonitorNumber = 1,
                Dpi = 96,
                MonitorRectDpiAware = new() { Left = 0, Top = 0, Width = 1920, Height = 1080 },
                MonitorRectDpiUnaware = new() { Left = 0, Top = 0, Width = 1920, Height = 1080 },
            },
        ],
        Applications = [],
    };

    private static async Task<object> RunAsync(string mode)
    {
        string legacy = WorkspacesStorage.GetLegacyFilePath();
        if (mode == "seed")
        {
            Directory.CreateDirectory(StorageIdentity.WorkspacesDirectory);
            using var file = new FileStream(legacy, FileMode.CreateNew, FileAccess.Write);
            file.Write(WorkspacesValidator.Serialize([Project("Local migration")]));
            return new { seeded = true };
        }

        var repository = new WorkspacesRepository();
        if (mode == "exercise")
        {
            var first = await repository.EnsureReadyAsync();
            if (first.Projects.Count != 1 || first.Projects[0].Name != "Local migration" || File.Exists(legacy))
            {
                throw new InvalidOperationException("First-use migration or matching-source cleanup failed.");
            }

            var projects = first.Projects.ToList();
            projects.Add(Project("Local saved"));
            await repository.SaveAsync(projects, first.Revision);
            var reopened = await new WorkspacesRepository().LoadAsync();
            if (reopened.Projects.Count != 2 || reopened.Projects[1].Name != "Local saved")
            {
                throw new InvalidOperationException("Saved workspaces did not survive a new repository instance.");
            }

            var preview = await repository.CreatePreviewAsync(Project("Local preview"));
            if ((await repository.ReadPreviewAsync(preview)).Name != "Local preview")
            {
                throw new InvalidOperationException("Transient preview round trip failed.");
            }

            await repository.ReleasePreviewAsync(preview);
            return new { migration = true, legacyDeleted = true, save = true, reopen = true, preview = true };
        }

        if (mode == "verify")
        {
            var reopened = await repository.LoadAsync();
            if (reopened.Projects.Count != 2 || !reopened.Projects.Any(project => project.Name == "Local saved"))
            {
                throw new InvalidOperationException("Cross-process persistence failed.");
            }

            using var identity = WindowsIdentity.GetCurrent();
            if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            {
                throw new InvalidOperationException("The direct-write probe requires a normal desktop token.");
            }

            string index = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                StorageIdentity.ProductName,
                "Data",
                identity.User!.Value,
                "storage.index");
            bool denied = false;
            try
            {
                using var handle = File.Open(index, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (UnauthorizedAccessException)
            {
                denied = true;
            }

            if (!denied)
            {
                throw new InvalidOperationException("The ordinary owner obtained direct storage write access.");
            }

            return new { persistedProjects = reopened.Projects.Count, directWriteDenied = true };
        }

        throw new ArgumentException("Supported modes: seed, exercise, verify.");
    }
}
