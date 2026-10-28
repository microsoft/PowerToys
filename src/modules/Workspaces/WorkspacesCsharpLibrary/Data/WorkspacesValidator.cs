// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>Validates the entire migration, import, or save before any storage mutation.</summary>
public static class WorkspacesValidator
{
    public const int MaximumBytes = 8 * 1024 * 1024;

    public static List<ProjectWrapper> Parse(ReadOnlyMemory<byte> bytes, bool preview = false, bool convertLegacy = false)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("The Workspaces document is empty or exceeds the size limit.");
        }

        if (convertLegacy && bytes.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            bytes = bytes[3..];
        }

        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        ValidateJson(document.RootElement);
        var projects = preview
            ? new List<ProjectWrapper> { ParseProject(document.RootElement, convertLegacy) }
            : ParseProjects(document.RootElement, convertLegacy);
        Validate(projects, preview);
        return projects;
    }

    public static byte[] Serialize(IReadOnlyList<ProjectWrapper> projects, bool preview = false)
    {
        Validate(projects, preview);
        byte[] bytes = preview
            ? JsonSerializer.SerializeToUtf8Bytes(projects[0], WorkspacesStorageJsonContext.Default.ProjectWrapper)
            : JsonSerializer.SerializeToUtf8Bytes(new WorkspacesData.WorkspacesListWrapper { Workspaces = projects.ToList() }, WorkspacesStorageJsonContext.Default.WorkspacesListWrapper);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("The Workspaces document exceeds the size limit.");
        }

        return bytes;
    }

    public static void Validate(IReadOnlyList<ProjectWrapper> projects, bool preview = false)
    {
        ArgumentNullException.ThrowIfNull(projects);
        Require(projects.Count <= 1024 && (!preview || projects.Count == 1));
        var ids = new HashSet<Guid>();
        foreach (var project in projects)
        {
            Require(TryIdentity(project.Id, out var id) && id != Guid.Empty && ids.Add(id));
            Text(project.Name, 256, preview);
            Require(project.Name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
            Require(project.CreationTime >= 0 && project.CreationTime <= 253402300799 && project.LastLaunchedTime >= 0 && project.LastLaunchedTime <= 253402300799);
            Require(project.Applications != null && project.Applications.Count <= 4096);
            Require(project.MonitorConfiguration != null && project.MonitorConfiguration.Count <= 128);
            var monitors = new HashSet<int>();
            foreach (var monitor in project.MonitorConfiguration)
            {
                Text(monitor.Id, 32767, false);
                Text(monitor.InstanceId, 32767, true);
                Require(monitor.MonitorNumber > 0 && monitors.Add(monitor.MonitorNumber) && monitor.Dpi is >= 48 and <= 9600);
                Rectangle(monitor.MonitorRectDpiAware.Left, monitor.MonitorRectDpiAware.Top, monitor.MonitorRectDpiAware.Width, monitor.MonitorRectDpiAware.Height);
                Rectangle(monitor.MonitorRectDpiUnaware.Left, monitor.MonitorRectDpiUnaware.Top, monitor.MonitorRectDpiUnaware.Width, monitor.MonitorRectDpiUnaware.Height);
            }

            var appIds = new HashSet<Guid>();
            foreach (var app in project.Applications)
            {
                Require(TryIdentity(app.Id, out var appId) && appId != Guid.Empty && appIds.Add(appId));
                Text(app.Application, 32767, false);
                Text(app.ApplicationPath, 32767, false);
                Require(Path.IsPathFullyQualified(app.ApplicationPath) && !app.ApplicationPath.StartsWith(@"\\?\", StringComparison.Ordinal) && !app.ApplicationPath.StartsWith(@"\\.\", StringComparison.Ordinal));
                Text(app.Title, 32767, true);
                Text(app.CommandLineArguments, 32767, true);
                Text(app.PackageFullName, 4096, true);
                Text(app.AppUserModelId, 4096, true);
                Require(app.PackageFullName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'));
                Require(app.AppUserModelId.StartsWith("steam://rungameid/", StringComparison.Ordinal)
                    ? app.AppUserModelId["steam://rungameid/".Length..].Length > 0 && app.AppUserModelId["steam://rungameid/".Length..].All(char.IsAsciiDigit)
                    : app.AppUserModelId.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '!'));
                Text(app.PwaAppId, 256, true);
                Text(app.Version, 16, true);
                Require(string.IsNullOrEmpty(app.Version) || (app.Version.All(char.IsAsciiDigit) && int.TryParse(app.Version, out var version) && version >= 0));
                Require(string.IsNullOrEmpty(app.PwaAppId) || app.PwaAppId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
                Require(monitors.Contains(app.Monitor));
                Rectangle(app.Position.X, app.Position.Y, app.Position.Width, app.Position.Height);
            }
        }
    }

    private static List<ProjectWrapper> ParseProjects(JsonElement root, bool convertLegacy)
    {
        Require(root.ValueKind == JsonValueKind.Object && root.TryGetProperty("workspaces", out _));
        Require(root.EnumerateObject().All(property => property.Name == "workspaces"));
        var array = root.GetProperty("workspaces");
        Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() <= 1024);
        return array.EnumerateArray().Select(value => ParseProject(value, convertLegacy)).ToList();
    }

    private static ProjectWrapper ParseProject(JsonElement value, bool convertLegacy)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        foreach (var required in new[] { "id", "name", "creation-time", "applications", "monitor-configuration" })
        {
            Require(value.TryGetProperty(required, out _));
        }

        Require(value.GetProperty("applications").ValueKind == JsonValueKind.Array);
        foreach (var app in value.GetProperty("applications").EnumerateArray())
        {
            Require(app.ValueKind == JsonValueKind.Object);
            foreach (var required in new[] { "application-path", "title", "command-line-arguments", "minimized", "maximized", "position", "monitor" })
            {
                Require(app.TryGetProperty(required, out _));
            }
        }

        Require(value.GetProperty("monitor-configuration").ValueKind == JsonValueKind.Array);
        foreach (var monitor in value.GetProperty("monitor-configuration").EnumerateArray())
        {
            Require(monitor.ValueKind == JsonValueKind.Object);
            foreach (var key in new[] { "monitor-rect-dpi-aware", "monitor-rect-dpi-unaware" })
            {
                Require(monitor.TryGetProperty(key, out var rectangle) && rectangle.ValueKind == JsonValueKind.Object);
                foreach (var required in new[] { "top", "left", "width", "height" })
                {
                    Require(rectangle.TryGetProperty(required, out _));
                }
            }
        }

        var project = value.Deserialize(WorkspacesStorageJsonContext.Default.ProjectWrapper);

        // Older Workspaces versions did not assign application IDs. Assign them once,
        // as part of the atomic conversion, never in the generic storage service.
        if (project.Applications != null)
        {
            for (int index = 0; index < project.Applications.Count; index++)
            {
                var app = project.Applications[index];
                if (convertLegacy && string.IsNullOrEmpty(app.Id))
                {
                    app.Id = Guid.NewGuid().ToString("B");
                }

                app.PackageFullName ??= string.Empty;
                app.AppUserModelId ??= string.Empty;
                app.PwaAppId ??= string.Empty;
                app.Version ??= string.Empty;
                project.Applications[index] = app;
            }
        }

        return project;
    }

    private static void ValidateJson(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name));
                Require(property.Name.All(c => c is >= ' ' and < '\x7f'));
                ValidateJson(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            Require(value.GetArrayLength() <= 4096);
            foreach (var element in value.EnumerateArray())
            {
                ValidateJson(element);
            }
        }
    }

    private static void Text(string value, int maximum, bool emptyAllowed)
    {
        Require(value != null && value.Length <= maximum && (emptyAllowed || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl));
    }

    private static bool TryIdentity(string value, out Guid id)
    {
        return Guid.TryParseExact(value, "B", out id) || Guid.TryParseExact(value, "D", out id);
    }

    private static void Rectangle(int x, int y, int width, int height)
    {
        Require(width > 0 && height > 0 && width <= 1000000 && height <= 1000000);
        Require((long)x + width <= int.MaxValue && (long)y + height <= int.MaxValue);
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidDataException("Invalid Workspaces document. No workspaces were imported or saved.");
        }
    }
}
