// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    [DoNotParallelize]
    public class EditorViewModelRegressionTests
    {
        private static readonly string[] ExpectedNameOrder = ["Middle", "Zulu"];

        [TestMethod]
        public void SaveProject_DelegatesRenameWithPreviousWorkspaceName()
        {
            using TestContext context = new();
            Project original = TestHelpers.CreateProject("Old", 0, 0, "Notepad");
            original.IsShortcutNeeded = true;
            context.Shortcuts.HasShortcut = true;
            context.ViewModel.Workspaces.Add(original);

            context.ViewModel.EditProject(original);
            Project edited = context.Navigation.Project;
            edited.Name = "New";
            context.ViewModel.SaveProject(edited);

            Assert.HasCount(1, context.Shortcuts.ApplyCalls);
            Assert.AreEqual((original.Id, "New", "Old", true), context.Shortcuts.ApplyCalls[0]);
            Assert.AreEqual(0, context.Shortcuts.RemoveCalls);
            Assert.AreEqual("New", original.Name);
            Assert.AreEqual(1, context.SerializedWorkspaces.Count);
        }

        [TestMethod]
        public void EditProject_DeletedShortcutIsNotRecreatedWhenSavingOtherSettings()
        {
            using TestContext context = new();
            Project original = TestHelpers.CreateProject("Workspace", 0, 0, "Notepad");
            original.IsShortcutNeeded = true;
            context.ViewModel.Workspaces.Add(original);

            context.ViewModel.EditProject(original);
            Project edited = context.Navigation.Project;
            Assert.IsFalse(edited.IsShortcutNeeded);
            edited.MoveExistingWindows = true;
            context.ViewModel.SaveProject(edited);

            Assert.IsTrue(original.MoveExistingWindows);
            Assert.IsFalse(original.IsShortcutNeeded);
            Assert.IsFalse(context.Shortcuts.ApplyCalls.Any(call => call.IsShortcutNeeded));
        }

        [TestMethod]
        public void SaveProject_UpdatesNameSortBeforeReturningToOverview()
        {
            using TestContext context = new(WorkspacesProperties.SortByProperty.Name);
            Project first = TestHelpers.CreateProject("Alpha", 0, 0, "Notepad");
            Project second = TestHelpers.CreateProject("Middle", 0, 0, "Notepad");
            context.ViewModel.Workspaces.Add(first);
            context.ViewModel.Workspaces.Add(second);
            context.ViewModel.RefreshWorkspacesView();
            context.ViewModel.EditProject(first);
            Project edited = context.Navigation.Project;
            edited.Name = "Zulu";

            context.ViewModel.SaveProject(edited);

            CollectionAssert.AreEqual(ExpectedNameOrder, context.ViewModel.WorkspacesView.Select(project => project.Name).ToArray());
        }

        [TestMethod]
        public void SaveProject_RecordsEditDeltasBeforeMutatingOriginal()
        {
            using TestContext context = new();
            Project original = TestHelpers.CreateProject("Workspace", 0, 0, "Notepad");
            context.ViewModel.Workspaces.Add(original);
            context.ViewModel.EditProject(original);
            Project edited = context.Navigation.Project;
            edited.Applications[0].CommandLineArguments = "--new-window";
            edited.Applications[0].IsElevated = true;
            edited.Applications[0].Position = new Application.WindowPosition { X = 100, Y = 200, Width = 800, Height = 600 };
            edited.IsPositionChangedManually = true;

            context.ViewModel.SaveProject(edited);

            Assert.AreEqual(1, context.EditEvents.Count);
            EditEvent telemetryEvent = context.EditEvents[0];
            Assert.AreEqual(1, telemetryEvent.CliAdded);
            Assert.AreEqual(1, telemetryEvent.AdminAdded);
            Assert.AreEqual(0, telemetryEvent.AppsAdded);
            Assert.AreEqual(0, telemetryEvent.AppsRemoved);
            Assert.IsTrue(telemetryEvent.PixelAdjustmentsUsed);
        }

        private sealed class TestContext : IDisposable
        {
            internal TestContext(WorkspacesProperties.SortByProperty sortBy = WorkspacesProperties.SortByProperty.LastLaunched)
            {
                WorkspacesSettings settings = new();
                settings.Properties.SortBy = sortBy;
                ViewModel = new MainViewModel(
                    new WorkspacesEditorIO(),
                    settings,
                    _ => { },
                    Shortcuts,
                    workspaces => SerializedWorkspaces.Add([.. workspaces]),
                    telemetryEvent => EditEvents.Add(telemetryEvent));
            }

            internal MainViewModel ViewModel { get; }

            internal FakeShortcutService Shortcuts { get; } = new();

            internal NavigationRecipient Navigation { get; } = new();

            internal List<List<Project>> SerializedWorkspaces { get; } = [];

            internal List<EditEvent> EditEvents { get; } = [];

            public void Dispose()
            {
                ViewModel.Dispose();
                Navigation.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        private sealed class NavigationRecipient : IDisposable
        {
            internal NavigationRecipient()
            {
                StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (recipient, message) => ((NavigationRecipient)recipient).Project = message.Project);
            }

            internal Project Project { get; private set; }

            public void Dispose()
            {
                StrongReferenceMessenger.Default.UnregisterAll(this);
                GC.SuppressFinalize(this);
            }
        }

        private sealed class FakeShortcutService : IWorkspaceShortcutService
        {
            internal bool HasShortcut { get; set; }

            internal List<(string ProjectId, string Name, string PreviousName, bool IsShortcutNeeded)> ApplyCalls { get; } = [];

            internal int RemoveCalls { get; private set; }

            public bool Exists(Project project) => HasShortcut;

            public void Apply(Project project, string previousName = null) =>
                ApplyCalls.Add((project.Id, project.Name, previousName, project.IsShortcutNeeded));

            public void Remove(Project project) => RemoveCalls++;
        }
    }
}
