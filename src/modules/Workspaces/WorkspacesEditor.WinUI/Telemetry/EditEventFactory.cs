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
            var updatedApps = updatedProject.Applications.Where(app => app.IsIncluded);
            EditEvent telemetryEvent = new()
            {
                Successful = true,
                ScreenCountDelta = updatedProject.Monitors.Count - previousProject.Monitors.Count,
                AppsRemoved = previousApps.Count,
                PixelAdjustmentsUsed = updatedProject.IsPositionChangedManually,
            };

            foreach (var (app, previous) in ApplicationWindowMatcher.Match(previousApps, updatedApps))
            {
                if (previous == null)
                {
                    telemetryEvent.AppsAdded++;
                    continue;
                }

                telemetryEvent.AppsRemoved--;
                bool previouslyHadArguments = !string.IsNullOrEmpty(previous.CommandLineArguments);
                bool hasArguments = !string.IsNullOrEmpty(app.CommandLineArguments);
                telemetryEvent.CliAdded += !previouslyHadArguments && hasArguments ? 1 : 0;
                telemetryEvent.CliRemoved += previouslyHadArguments && !hasArguments ? 1 : 0;
                telemetryEvent.AdminAdded += !previous.IsElevated && app.IsElevated ? 1 : 0;
                telemetryEvent.AdminRemoved += previous.IsElevated && !app.IsElevated ? 1 : 0;
            }

            return telemetryEvent;
        }
    }
}
