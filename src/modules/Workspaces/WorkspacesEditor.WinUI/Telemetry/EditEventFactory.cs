// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.Telemetry
{
    internal static class EditEventFactory
    {
        internal static EditEvent Create(Project previousProject, Project updatedProject)
        {
            ArgumentNullException.ThrowIfNull(previousProject);
            ArgumentNullException.ThrowIfNull(updatedProject);

            var previousApps = previousProject.Applications.Where(app => app.IsIncluded).ToList();
            var updatedApps = updatedProject.Applications.Where(app => app.IsIncluded).ToList();
            EditEvent telemetryEvent = new()
            {
                Successful = true,
                ScreenCountDelta = updatedProject.Monitors.Count - previousProject.Monitors.Count,
                AppsAdded = updatedApps.Count(app => !previousApps.Any(previous => IsSameApplication(previous, app))),
                AppsRemoved = previousApps.Count(app => !updatedApps.Any(updated => IsSameApplication(app, updated))),
                PixelAdjustmentsUsed = updatedProject.IsPositionChangedManually,
            };

            foreach (Application app in updatedApps)
            {
                Application previous = previousApps.FirstOrDefault(candidate => IsSameApplication(candidate, app));
                if (previous == null)
                {
                    continue;
                }

                bool previouslyHadArguments = !string.IsNullOrEmpty(previous.CommandLineArguments);
                bool hasArguments = !string.IsNullOrEmpty(app.CommandLineArguments);
                telemetryEvent.CliAdded += !previouslyHadArguments && hasArguments ? 1 : 0;
                telemetryEvent.CliRemoved += previouslyHadArguments && !hasArguments ? 1 : 0;
                telemetryEvent.AdminAdded += !previous.IsElevated && app.IsElevated ? 1 : 0;
                telemetryEvent.AdminRemoved += previous.IsElevated && !app.IsElevated ? 1 : 0;
            }

            return telemetryEvent;
        }

        private static bool IsSameApplication(Application previous, Application updated)
        {
            return !string.IsNullOrEmpty(previous.Id) && string.Equals(previous.Id, updated.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
