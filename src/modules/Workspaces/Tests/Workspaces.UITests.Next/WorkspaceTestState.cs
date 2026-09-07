// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class WorkspaceTestState : IDisposable
    {
        private readonly List<IDisposable> snapshots = [];
        private readonly HashSet<string> trackedFiles = new(StringComparer.OrdinalIgnoreCase);
        private int applicationIndex;

        internal WorkspaceTestState()
        {
            Preserve(SettingsPath);
            Preserve(WorkspacesPath);
            Preserve(TemporaryPath);
        }

        internal static string DirectoryPath => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "Workspaces");

        internal static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");

        internal static string WorkspacesPath => Path.Combine(DirectoryPath, "workspaces.json");

        internal static string TemporaryPath => Path.Combine(DirectoryPath, "temp-workspaces.json");

        internal TestAppFixture Fixture { get; } = new();

        internal string Prefix { get; private set; } = string.Empty;

        internal void PrepareSettings()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(SettingsPath, """
                {
                  "name": "Workspaces",
                  "version": "0.0.1",
                  "properties": {
                    "hotkey": {
                      "value": {
                        "win": true,
                        "ctrl": true,
                        "shift": true,
                        "alt": false,
                        "code": 87,
                        "key": "W"
                      }
                    },
                    "sortby": 0
                  }
                }
                """);

            // UITestBase has already journaled the global settings before this pre-launch hook.
            var globalPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
            var global = ReadObject(globalPath);
            global["enable_quick_access"] = true;
            global["run_elevated"] = false;
            File.WriteAllText(globalPath, global.ToJsonString());
        }

        internal void Reset()
        {
            Prefix = $"PTUITest-{Guid.NewGuid():N}";
            applicationIndex = 0;
            File.Delete(TemporaryPath);
            WriteProjects();
        }

        internal JsonObject Application(string name, string title, string? arguments = null, string? path = null)
        {
            applicationIndex++;
            return new JsonObject
            {
                // Deterministic, ordering-stable application IDs. The value keeps its ascending index in
                // the final GUID group so the native launcher's ID-keyed std::map iterates in a predictable
                // order, and pins a non-zero first group so neither 8-byte half of the GUID is all zero:
                // WorkspacesWindowProperties::GetGuidFromHwnd treats a zero half as "unstamped" and returns
                // an empty ID, which would break Launch-and-Edit identity round-tripping for a window whose
                // saved ID had a zero half.
                ["id"] = $"{{00000001-0000-0000-0000-{applicationIndex.ToString("x12", CultureInfo.InvariantCulture)}}}",
                ["application"] = name,
                ["application-path"] = path ?? Fixture.ExecutablePath,
                ["package-full-name"] = string.Empty,
                ["app-user-model-id"] = string.Empty,
                ["pwa-app-id"] = string.Empty,
                ["title"] = title,
                ["command-line-arguments"] = arguments ?? $"--title \"{title}\"",
                ["is-elevated"] = false,
                ["can-launch-elevated"] = true,
                ["minimized"] = false,
                ["maximized"] = false,
                ["position"] = Position(240, 220, 720, 460),
                ["monitor"] = 1,
                ["version"] = "1",
            };
        }

        internal JsonObject Project(string suffix, IntPtr settingsWindow, params JsonObject[] applications)
        {
            var name = $"{Prefix}-{suffix}";
            var id = Guid.NewGuid().ToString("B");
            Preserve(Path.Combine(DirectoryPath, "WorkspacesIcons", id + ".ico"));
            TrackShortcut(name);
            var monitor = MonitorInfo.GetPrimary() ?? throw new InvalidOperationException("No primary monitor is available.");
            var dpi = NativeMethods.GetDpiForWindow(settingsWindow);
            Assert.IsTrue(dpi > 0, "The Settings window did not report a usable DPI.");
            var scale = dpi / 96.0;
            return new JsonObject
            {
                ["id"] = id,
                ["name"] = name,
                ["creation-time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600,
                ["last-launched-time"] = 0,
                ["is-shortcut-needed"] = false,
                ["move-existing-windows"] = false,
                ["monitor-configuration"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = monitor.DeviceName,
                        ["instance-id"] = string.Empty,
                        ["monitor-number"] = 1,
                        ["dpi"] = dpi,
                        ["monitor-rect-dpi-aware"] = MonitorRectangle(monitor.Left, monitor.Top, monitor.Width, monitor.Height),
                        ["monitor-rect-dpi-unaware"] = MonitorRectangle(
                            (int)Math.Round(monitor.Left / scale),
                            (int)Math.Round(monitor.Top / scale),
                            (int)Math.Round(monitor.Width / scale),
                            (int)Math.Round(monitor.Height / scale)),
                    },
                },
                ["applications"] = new JsonArray(applications.Select(application => application.DeepClone()).ToArray()),
            };
        }

        internal static JsonObject Position(int x, int y, int width, int height) => new()
        {
            ["X"] = x,
            ["Y"] = y,
            ["width"] = width,
            ["height"] = height,
        };

        internal void WriteProjects(params JsonObject[] projects)
        {
            Directory.CreateDirectory(DirectoryPath);
            var root = new JsonObject
            {
                ["workspaces"] = new JsonArray(projects.Select(project => project.DeepClone()).ToArray()),
            };
            File.WriteAllText(WorkspacesPath, root.ToJsonString());
        }

        internal void TrackShortcut(string name) => Preserve(ShortcutPath(name));

        internal static string ShortcutPath(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");

        internal static JsonObject ReadObject(string path) =>
            JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException($"No JSON object was read from '{path}'.");

        internal static JsonArray ReadProjects() =>
            ReadObject(WorkspacesPath)["workspaces"]?.AsArray() ?? throw new InvalidDataException("The workspaces array is missing.");

        internal static JsonObject ReadProject(string id) =>
            ReadProjects().Select(node => node!.AsObject()).Single(project => Text(project, "id") == id);

        internal static string Text(JsonNode node, string property) =>
            node[property]?.GetValue<string>() ?? throw new InvalidDataException($"Required string '{property}' is missing.");

        internal static JsonObject WaitForProject(string id, Func<JsonObject, bool> predicate)
        {
            var result = WaitHelper.WaitForStable(
                () => ReadProjects().Select(node => node!.AsObject()).SingleOrDefault(project => Text(project, "id") == id),
                project => project is not null && predicate(project),
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                shouldRetryException: error => error is IOException or JsonException);
            Assert.IsTrue(
                result.Succeeded,
                $"Workspace '{id}' did not persist the expected state. Last: {result.LastObservation}; error: {result.LastException?.Message}");
            return result.LastObservation!;
        }

        public void Dispose()
        {
            try
            {
                Fixture.Dispose();
            }
            finally
            {
                List<Exception> errors = [];
                foreach (var snapshot in snapshots.AsEnumerable().Reverse())
                {
                    try
                    {
                        snapshot.Dispose();
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        errors.Add(error);
                    }
                }

                snapshots.Clear();
                if (errors.Count > 0)
                {
                    throw new AggregateException("Could not restore the Workspaces test files.", errors);
                }
            }
        }

        private static JsonObject MonitorRectangle(int left, int top, int width, int height) => new()
        {
            ["top"] = top,
            ["left"] = left,
            ["width"] = width,
            ["height"] = height,
        };

        private void Preserve(string path)
        {
            if (trackedFiles.Add(path))
            {
                snapshots.Add(SettingsConfigHelper.PreserveFile(path));
            }
        }
    }
}
