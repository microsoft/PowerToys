// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Shared helpers for creating test fixtures.
    /// Constructs Project and Application objects via the same constructors
    /// used in production (ProjectWrapper deserialization path).
    /// </summary>
    internal static class TestHelpers
    {
        internal static MainViewModel CreateViewModel()
        {
            return new MainViewModel(new Utils.WorkspacesEditorIO());
        }

        internal static Project CreateProject(string name, long creationTime = 0, long lastLaunchedTime = 0, params string[] appNames)
        {
            var appWrappers = appNames.Select(n => new ApplicationWrapper
            {
                Application = n,
                ApplicationPath = $@"C:\{n}.exe",
                Title = string.Empty,
                PackageFullName = string.Empty,
                AppUserModelId = string.Empty,
                PwaAppId = string.Empty,
                CommandLineArguments = string.Empty,
                IsElevated = false,
                CanLaunchElevated = false,
                Minimized = false,
                Maximized = false,
                Position = default,
                Monitor = 0,
            }).ToList();

            var projectWrapper = new ProjectWrapper
            {
                Id = $"{{{Guid.NewGuid()}}}",
                Name = name,
                CreationTime = creationTime,
                LastLaunchedTime = lastLaunchedTime,
                IsShortcutNeeded = false,
                MoveExistingWindows = false,
                Applications = appWrappers,
                MonitorConfiguration = new List<MonitorConfigurationWrapper>(),
            };

            return new Project(projectWrapper);
        }
    }
}
