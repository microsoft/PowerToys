// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;
using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using EnvironmentVariablesUILib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.ViewModels;

[TestClass]
[DoNotParallelize]
public class EnvironmentVariablesPageActionsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [DataTestMethod]
    [DataRow("edit", false)]
    [DataRow("edit", true)]
    [DataRow("default", false)]
    [DataRow("default", true)]
    [DataRow("add-profile", false)]
    [DataRow("update-profile", false)]
    [DataRow("add-variable", false)]
    [DataRow("cancel", false)]
    public void SyncCommands_CanExecuteRemainsTrueWithoutDomainReads(string kind, bool nullParameter)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel(false);
        var system = vm.SystemDefaultSet;
        var reads = 0;
        object Read()
        {
            reads++;
            throw new InvalidOperationException("Domain read during command query.");
        }

        var actions = new EnvironmentVariablesPageActions(
            () => (MainViewModel)Read(),
            Read,
            Read,
            Read,
            () => throw new InvalidOperationException("Unexpected show."),
            () => Read(),
            () => Read());
        ICommand Get() => kind switch
        {
            "edit" => actions.EditCommand,
            "default" => actions.AddDefaultVariableCommand,
            "add-profile" => actions.AddProfileCommand,
            "update-profile" => actions.UpdateProfileCommand,
            "add-variable" => actions.AddVariableCommand,
            _ => actions.CancelAddVariableCommand,
        };
        var first = Get();
        var second = Get();
        object? parameter = nullParameter ? null : kind switch
        {
            "edit" => new EnvironmentVariablesPageActions.EditTarget(new Variable("invalid=name", "old", VariablesSetType.System), system),
            "default" => system,
            _ => null,
        };

        Assert.AreNotSame(first, second);
        Assert.IsTrue(first.CanExecute(parameter));
        Assert.IsTrue(second.CanExecute(parameter));
        Assert.AreEqual(0, reads);
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [TestMethod]
    public async Task NewProfile_CanExecuteTracksOnlyItsOwnPendingTask()
    {
        var effects = new List<string>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProfileVariablesSet? draft = null;
        var actions = new EnvironmentVariablesPageActions(
            () => throw new InvalidOperationException("VM read"),
            () => throw new InvalidOperationException("Edit read"),
            () => throw new InvalidOperationException("Profile read"),
            () => throw new InvalidOperationException("Default read"),
            () => EnvironmentVariablesPageActions.ShowNewProfileAsync(
                () => effects.Add("select"),
                () => effects.Add("configure"),
                value =>
                {
                    draft = value;
                    effects.Add("draft");
                },
                () =>
                {
                    effects.Add("show");
                    return completion.Task;
                }),
            () => throw new InvalidOperationException("Add"),
            () => throw new InvalidOperationException("Cancel"));
        var first = (IAsyncRelayCommand)actions.NewProfileCommand;
        Assert.IsTrue(first.CanExecute(null));
        var execution = first.ExecuteAsync(null);
        try
        {
            Assert.IsFalse(first.CanExecute(null));
            var second = actions.NewProfileCommand;
            Assert.AreNotSame(first, second);
            Assert.IsTrue(second.CanExecute(null));
            var expectedDialogOrder = new[] { "select", "configure", "draft", "show" };
            CollectionAssert.AreEqual(expectedDialogOrder, effects);
            Assert.IsNotNull(draft);
            Assert.AreNotEqual(Guid.Empty, draft.Id);
            Assert.AreEqual(string.Empty, draft.Name);
            Assert.AreEqual(0, draft.Variables.Count);
        }
        finally
        {
            completion.TrySetResult(true);
            await execution.WaitAsync(Timeout);
        }

        Assert.IsTrue(first.CanExecute(null));
        Assert.AreEqual(4, effects.Count);
    }

    [TestMethod]
    public async Task ProfileCommands_ReadCurrentDraftAndVmAndSerializeActualAddThenUpdate()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var oldVm = fixture.CreateViewModel(service: service);
        var currentVm = oldVm;
        oldVm.Profiles = new();
        object draft = Profile("unused");
        var effects = new List<string>();
        var actions = new EnvironmentVariablesPageActions(
            () =>
            {
                effects.Add("vm");
                return currentVm;
            },
            () => throw new InvalidOperationException(),
            () =>
            {
                effects.Add("draft");
                return draft;
            },
            () => throw new InvalidOperationException(),
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var add = actions.AddProfileCommand;
        var update = actions.UpdateProfileCommand;
        Assert.AreEqual(0, effects.Count);
        currentVm = fixture.CreateViewModel(service: service);
        currentVm.Profiles = new();
        var added = Profile("latest");
        draft = added;

        add.Execute(null);

        Assert.AreSame(added, currentVm.Profiles.Single());
        Assert.AreEqual(0, oldVm.Profiles.Count);
        var expectedAddReadOrder = new[] { "draft", "vm" };
        CollectionAssert.AreEqual(expectedAddReadOrder, effects);
        await fixture.Work.CompleteSaveAsync(0);
        var firstJson = files.Writes.Single().Contents!;
        Assert.AreEqual("latest", JsonSerializer.Deserialize<List<ProfileVariablesSet>>(firstJson)!.Single().Name);
        var replacement = Profile("updated");
        replacement.Id = added.Id;
        replacement.Variables.Add(new Variable("PATH", "one;;two;", VariablesSetType.Profile));
        draft = replacement;
        effects.Clear();

        var savesBeforeUpdate = fixture.Work.SaveCount;
        var notifications = new List<string?>();
        PropertyChangedEventHandler changed = (_, args) => notifications.Add(args.PropertyName);
        added.PropertyChanged += changed;
        try
        {
            update.Execute(null);
        }
        finally
        {
            added.PropertyChanged -= changed;
        }

        Assert.AreSame(added, currentVm.Profiles.Single());
        Assert.AreSame(replacement.Variables, added.Variables);
        Assert.AreEqual("updated", added.Name);
        var expectedUpdateReadOrder = new[] { "draft", "vm" };
        CollectionAssert.AreEqual(expectedUpdateReadOrder, effects);
        var expectedNotificationOrder = new[] { nameof(ProfileVariablesSet.Name), nameof(ProfileVariablesSet.Valid), nameof(ProfileVariablesSet.Variables) };
        CollectionAssert.AreEqual(expectedNotificationOrder, notifications);
        Assert.AreEqual(5, fixture.Work.SaveCount);
        Assert.AreEqual(4, fixture.Work.SaveCount - savesBeforeUpdate);
        for (var index = 1; index < fixture.Work.SaveCount; index++)
        {
            await fixture.Work.CompleteSaveAsync(index);
            var updated = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(files.Writes[index].Contents!)!.Single();
            Assert.AreEqual(added.Id, updated.Id);
            Assert.AreEqual("updated", updated.Name);
            Assert.IsFalse(updated.IsEnabled);
            Assert.AreEqual(1, updated.Variables.Count);
            Assert.AreEqual("PATH", updated.Variables.Single().Name);
            Assert.AreEqual("one;;two;", updated.Variables.Single().Values);
        }

        Assert.AreEqual(5, files.Writes.Count);
        var saved = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(files.Writes[1].Contents!)!.Single();
        Assert.AreEqual("one;;two;", saved.Variables.Single().Values);
        Assert.AreEqual("latest", JsonSerializer.Deserialize<List<ProfileVariablesSet>>(firstJson)!.Single().Name);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [TestMethod]
    public async Task UpdateCommand_MissingIdentitySavesWithoutInsertingAndAddNullFailsBeforeSave()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        var existing = Profile("existing");
        vm.Profiles = new() { existing };
        object draft = Profile("missing");
        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => null!,
            () => draft,
            () => null!,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });

        actions.UpdateProfileCommand.Execute(null);

        Assert.AreSame(existing, vm.Profiles.Single());
        Assert.AreEqual("existing", existing.Name);
        await fixture.Work.CompleteSaveAsync(0);
        Assert.AreSame(existing, fixture.Service.Writes.Single().Single());
        draft = new object();
        Assert.ThrowsException<NullReferenceException>(() => actions.AddProfileCommand.Execute(null));
        Assert.AreEqual(1, fixture.Work.SaveCount);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CommandNullParameters_PreserveReadOrderBeforeThrow(bool defaultAdd)
    {
        var effects = new List<string>();
        var actions = new EnvironmentVariablesPageActions(
            () =>
            {
                effects.Add("vm");
                throw new InvalidOperationException();
            },
            () =>
            {
                effects.Add("edit");
                return null!;
            },
            () => null!,
            () =>
            {
                effects.Add("default");
                return null!;
            },
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });

        Assert.ThrowsException<NullReferenceException>(() =>
        (defaultAdd ? actions.AddDefaultVariableCommand : actions.EditCommand).Execute(null));

        var expectedDispatchOrder = defaultAdd ? new[] { "default" } : Array.Empty<string>();
        CollectionAssert.AreEqual(expectedDispatchOrder, effects);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UpdateNullDraft_EmptyProfilesSchedulesSaveButPopulatedProfilesThrows(bool populated)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        vm.Profiles = new();
        if (populated)
        {
            vm.Profiles.Add(Profile("present"));
        }

        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => null!,
            () => new object(),
            () => null!,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        if (populated)
        {
            Assert.ThrowsException<NullReferenceException>(() => actions.UpdateProfileCommand.Execute(null));
            Assert.AreEqual(0, fixture.Work.SaveCount);
        }
        else
        {
            actions.UpdateProfileCommand.Execute(null);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual(0, fixture.Service.Writes.Single().Length);
        }

        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [TestMethod]
    public void EditCommand_DefaultSetIsNotProfileAndUnchangedDraftDoesNotSchedule()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel(false);
        var original = new Variable("SYSTEM", "old", VariablesSetType.System);
        object draft = original.Clone();
        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => draft,
            () => null!,
            () => null!,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var command = actions.EditCommand;
        var target = new EnvironmentVariablesPageActions.EditTarget(original, vm.SystemDefaultSet);

        command.Execute(target);

        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        draft = new object();
        Assert.ThrowsException<NullReferenceException>(() => command.Execute(target));
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        Assert.AreEqual(0, fixture.Environment.Opens.Count);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProfileEdit_SelectsBeforeReadAndUsesShallowCloneWithoutCommit(bool missing)
    {
        var effects = new List<string>();
        var target = Profile("original");
        target.Variables.Add(new Variable("A", "old", VariablesSetType.Profile));
        ProfileVariablesSet? draft = null;
        await EnvironmentVariablesPageActions.ShowProfileEditAsync(
            () => effects.Add("select"),
            () =>
            {
                effects.Add("read");
                return missing ? null! : target;
            },
            () => effects.Add("configure"),
            value =>
            {
                effects.Add("draft");
                draft = value;
            },
            () =>
            {
                effects.Add("show");
                return Task.CompletedTask;
            }).WaitAsync(Timeout);

        var expectedEditOrder = missing ? new[] { "select", "read" } : new[] { "select", "read", "configure", "draft", "show" };
        CollectionAssert.AreEqual(expectedEditOrder, effects);
        if (!missing)
        {
            Assert.IsNotNull(draft);
            Assert.AreNotSame(target, draft);
            Assert.AreEqual(target.Id, draft.Id);
            Assert.AreNotSame(target.Variables, draft.Variables);
            Assert.AreSame(target.Variables[0], draft.Variables[0]);
            Assert.AreEqual("original", target.Name);
        }
    }

    [TestMethod]
    public async Task VariableDialog_ClonesRawFieldsAndCommandCommitsLiveDraftBeforeShowCompletes()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var vm = fixture.CreateViewModel(service: service);
        var profile = Profile("profile");
        var original = new Variable("PATH", "old;;tail;", VariablesSetType.Profile)
        {
            ApplyToSystem = true,
            IsAppliedFromProfile = true,
        };
        original.ValuesList[0].Text = "stale";
        profile.Variables.Add(original);
        vm.Profiles = new() { profile };
        object? draft = null;
        var effects = new List<string>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => draft!,
            () => profile,
            () => null!,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var command = actions.EditCommand;
        var show = EnvironmentVariablesPageActions.ShowVariableEditAsync(
            original,
            () => effects.Add("configure"),
            value =>
            {
                draft = value;
                effects.Add("draft");
            },
            () => effects.Add("gate"),
            () =>
            {
                effects.Add("show");
                return completion.Task;
            });
        try
        {
            var clone = (Variable)draft!;
            var expectedShowOrder = new[] { "configure", "draft", "gate", "show" };
            CollectionAssert.AreEqual(expectedShowOrder, effects);
            Assert.AreNotSame(original, clone);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.AreEqual(original.ParentType, clone.ParentType);
            CollectionAssert.AreEqual(new[] { "old", string.Empty, "tail", string.Empty }, clone.ValuesList.Select(x => x.Text).ToArray());
            Assert.AreNotSame(original.ValuesList[0], clone.ValuesList[0]);
            Assert.IsFalse(clone.ApplyToSystem);
            Assert.IsFalse(clone.IsAppliedFromProfile);
            clone.Values = "new;;end;";
            Assert.AreEqual("old;;tail;", original.Values);

            command.Execute(new EnvironmentVariablesPageActions.EditTarget(original, profile));

            Assert.IsFalse(show.IsCompleted);
            Assert.AreEqual("new;;end;", original.Values);
            Assert.IsTrue(original.ApplyToSystem);
            Assert.IsTrue(original.IsAppliedFromProfile);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            await fixture.Work.CompleteActionAsync(0);
            fixture.Work.DrainRefreshes();
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual("new;;end;", JsonSerializer.Deserialize<List<ProfileVariablesSet>>(files.Writes.Single().Contents!)!.Single().Variables.Single().Values);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }
        finally
        {
            completion.TrySetResult(true);
            await show.WaitAsync(Timeout);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DefaultDialog_UsesIdForDraftScopeButCommandUsesLiveDraft(bool systemId)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel(true);
        var set = new DefaultVariablesSet(
            systemId ? VariablesSet.SystemGuid : VariablesSet.UserGuid,
            "mismatch",
            systemId ? VariablesSetType.User : VariablesSetType.System);
        object? draft = null;
        var effects = new List<string>();
        var actions = new EnvironmentVariablesPageActions(
            () =>
            {
                effects.Add("vm");
                return vm;
            },
            () => null!,
            () => null!,
            () =>
            {
                effects.Add("read-draft");
                return draft!;
            },
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var command = actions.AddDefaultVariableCommand;
        await EnvironmentVariablesPageActions.ShowDefaultAddAsync(
            set,
            () => effects.Add("configure"),
            value =>
            {
                draft = value;
                effects.Add("draft");
            },
            () => effects.Add("gate"),
            () =>
            {
                effects.Add("show");
                return Task.CompletedTask;
            }).WaitAsync(Timeout);
        var created = (Variable)draft!;
        Assert.AreEqual(systemId ? VariablesSetType.System : VariablesSetType.User, created.ParentType);
        Assert.AreEqual(string.Empty, created.Name);
        Assert.AreEqual(string.Empty, created.Values);
        var expectedShowOrder = new[] { "configure", "draft", "gate", "show" };
        CollectionAssert.AreEqual(expectedShowOrder, effects);
        draft = new Variable("LATEST", "raw;;value", created.ParentType);
        effects.Clear();
        var routes = new List<VariablesSetType>();
        var userVariables = vm.UserDefaultSet.Variables;
        var systemVariables = vm.SystemDefaultSet.Variables;
        NotifyCollectionChangedEventHandler userAdded = (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && ReferenceEquals(args.NewItems![0], draft))
            {
                routes.Add(VariablesSetType.User);
            }
        };
        NotifyCollectionChangedEventHandler systemAdded = (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && ReferenceEquals(args.NewItems![0], draft))
            {
                routes.Add(VariablesSetType.System);
            }
        };
        userVariables.CollectionChanged += userAdded;
        systemVariables.CollectionChanged += systemAdded;
        try
        {
            command.Execute(set);
        }
        finally
        {
            userVariables.CollectionChanged -= userAdded;
            systemVariables.CollectionChanged -= systemAdded;
        }

        var expectedCommitReadOrder = new[] { "read-draft", "vm" };
        CollectionAssert.AreEqual(expectedCommitReadOrder, effects);
        CollectionAssert.AreEqual(new[] { set.Type }, routes);
        Assert.AreEqual("raw;;value", (systemId ? fixture.Environment.Machine : fixture.Environment.User)["LATEST"]);
        Assert.IsFalse((systemId ? fixture.Environment.User : fixture.Environment.Machine).ContainsKey("LATEST"));
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [DataTestMethod]
    [DataRow(VariablesSetType.System, "VALID")]
    [DataRow(VariablesSetType.User, "invalid=name")]
    public void DefaultCommand_DirectExecutionUsesHelperScopeAndValidationWithoutElevationGate(VariablesSetType scope, string name)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel(false);
        var draft = new Variable(name, "raw", scope);
        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => null!,
            () => null!,
            () => draft,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var command = actions.AddDefaultVariableCommand;

        Assert.IsTrue(command.CanExecute(vm.SystemDefaultSet));
        command.Execute(scope == VariablesSetType.User ? vm.UserDefaultSet : vm.SystemDefaultSet);

        Assert.AreEqual(0, fixture.Environment.User.Count);
        if (scope == VariablesSetType.System)
        {
            Assert.AreEqual(1, fixture.Environment.Machine.Count);
            Assert.AreEqual("raw", fixture.Environment.Machine["VALID"]);
            var writes = fixture.Environment.Operations.Where(x => x.Operation == "Write").ToList();
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(RegistryHive.LocalMachine, writes[0].Hive);
            Assert.AreEqual("VALID", writes[0].Name);
            var (value, kind) = ((object, RegistryValueKind))writes[0].Value!;
            Assert.AreEqual("raw", value);
            Assert.AreEqual(RegistryValueKind.String, kind);
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            var published = vm.SystemDefaultSet.Variables.Single();
            Assert.AreNotSame(draft, published);
            Assert.AreEqual((name, "raw", scope), (published.Name, published.Values, published.ParentType));
        }
        else
        {
            Assert.AreEqual(0, fixture.Environment.Machine.Count);
            Assert.AreEqual(0, fixture.Environment.Operations.Count(x => x.Operation == "Write"));
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, vm.UserDefaultSet.Variables.Count);
        }

        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [TestMethod]
    public async Task DialogFailures_PreserveConfigurePrecedenceAndStopPublication()
    {
        var failure = new InvalidOperationException("configure");
        var effects = new List<string>();
        var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.ShowVariableEditAsync(
            null!,
            () => throw failure,
            _ => effects.Add("draft"),
            () => effects.Add("gate"),
            () =>
            {
                effects.Add("show");
                return Task.CompletedTask;
            }));
        Assert.AreSame(failure, observed);
        Assert.AreEqual(0, effects.Count);
        await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
        EnvironmentVariablesPageActions.ShowDefaultAddAsync(
            null!,
            () => effects.Add("configure"),
            _ => effects.Add("draft"),
            () => effects.Add("gate"),
            () =>
            {
                effects.Add("show");
                return Task.CompletedTask;
            }));
        var expectedConfigurationOrder = new[] { "configure" };
        CollectionAssert.AreEqual(expectedConfigurationOrder, effects);
    }

    [DataTestMethod]
    [DataRow("publish")]
    [DataRow("gate")]
    [DataRow("show")]
    public async Task VariableDialog_FaultsRetainOnlyEarlierEffectsWithoutCleanup(string failingEffect)
    {
        var original = new Variable("A", "old", VariablesSetType.User);
        var effects = new List<string>();
        var failure = new InvalidOperationException(failingEffect);
        void Effect(string effect)
        {
            effects.Add(effect);
            if (effect == failingEffect)
            {
                throw failure;
            }
        }

        var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.ShowVariableEditAsync(
            original,
            () => Effect("configure"),
            _ => Effect("publish"),
            () => Effect("gate"),
            () =>
            {
                Effect("show");
                return Task.CompletedTask;
            }));
        Assert.AreSame(failure, observed);
        var order = new[] { "configure", "publish", "gate", "show" };
        CollectionAssert.AreEqual(order.Take(Array.IndexOf(order, failingEffect) + 1).ToArray(), effects);
        Assert.AreEqual("old", original.Values);
    }

    [TestMethod]
    public async Task ProfileDialog_SelectionFaultPreventsReadAndShowCompletionNeverAdds()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        vm.Profiles = new();
        var effects = new List<string>();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.ShowProfileEditAsync(
            () => throw new InvalidOperationException("selection"),
            () =>
            {
                effects.Add("read");
                return Profile("target");
            },
            () => effects.Add("configure"),
            _ => effects.Add("draft"),
            () =>
            {
                effects.Add("show");
                return Task.CompletedTask;
            }));
        Assert.AreEqual(0, effects.Count);
        ProfileVariablesSet? draft = null;
        await EnvironmentVariablesPageActions.ShowNewProfileAsync(
            () => effects.Add("select"),
            () => effects.Add("configure"),
            value => draft = value,
            () => Task.CompletedTask).WaitAsync(Timeout);
        Assert.IsNotNull(draft);
        Assert.AreEqual(0, vm.Profiles.Count);
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Confirmation_UsesLiveVmAndTitleAndOnlyPrimaryMutatesBeforeCompletion(bool deleteVariable, bool firePrimary)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var first = fixture.CreateViewModel();
        var second = fixture.CreateViewModel();
        var target = Profile("before");
        var variable = new Variable("BEFORE", "raw", VariablesSetType.Profile);
        target.Variables.Add(variable);
        first.Profiles = new();
        second.Profiles = new() { target };
        var current = first;
        var reads = 0;
        Func<string>? title = null;
        Action? primary = null;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Present(Func<string> readTitle, Action callback)
        {
            title = readTitle;
            primary = callback;
            return completion.Task;
        }

        MainViewModel ReadVm()
        {
            reads++;
            return current;
        }

        var showing = deleteVariable
        ? EnvironmentVariablesPageActions.ConfirmDeleteVariableAsync(ReadVm, variable, target, Present)
        : EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(ReadVm, target, Present);
        try
        {
            Assert.AreEqual(0, reads);
            current = second;
            target.Name = "after";
            variable.Name = "AFTER";
            Assert.AreEqual(deleteVariable ? "AFTER" : "after", title!());
            if (firePrimary)
            {
                primary!();
                Assert.IsFalse(showing.IsCompleted);
                Assert.AreEqual(1, reads);
                Assert.AreEqual(deleteVariable ? 0 : 1, target.Variables.Count);
                Assert.AreEqual(deleteVariable ? 1 : 0, second.Profiles.Count);
                Assert.AreEqual(1, fixture.Work.SaveCount);
            }
        }
        finally
        {
            completion.TrySetResult(true);
            await showing.WaitAsync(Timeout);
        }

        Assert.AreEqual(firePrimary ? 1 : 0, reads);
        Assert.AreEqual(firePrimary ? 1 : 0, fixture.Work.SaveCount);
        Assert.AreEqual(0, fixture.Work.ActionCount);
        if (firePrimary)
        {
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual(deleteVariable ? 1 : 0, fixture.Service.Writes.Single().Length);
        }

        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [TestMethod]
    public async Task Confirmation_NullTargetsSkipPresenterAndRepeatedPrimaryKeepsDuplicateDispatch()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        var profile = Profile("target");
        vm.Profiles = new() { profile };
        var reads = 0;
        MainViewModel ReadVm()
        {
            reads++;
            return vm;
        }

        Task Unexpected(Func<string> title, Action primary) => throw new InvalidOperationException("presenter");
        await EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(ReadVm, null!, Unexpected).WaitAsync(Timeout);
        await EnvironmentVariablesPageActions.ConfirmDeleteVariableAsync(ReadVm, null!, profile, Unexpected).WaitAsync(Timeout);
        Assert.AreEqual(0, reads);

        await EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(
            ReadVm,
            profile,
            (_, primary) =>
            {
                primary();
                primary();
                return Task.CompletedTask;
            }).WaitAsync(Timeout);

        Assert.AreEqual(2, reads);
        Assert.AreEqual(0, vm.Profiles.Count);
        Assert.AreEqual(2, fixture.Work.SaveCount);
        await fixture.Work.CompleteSaveAsync(0);
        await fixture.Work.CompleteSaveAsync(1);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Confirmation_PresenterFailureDoesNotRollbackCompletedPrimary(bool firePrimary)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        var profile = Profile("target");
        vm.Profiles = new() { profile };
        var failure = new InvalidOperationException("presenter");
        var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(
            () => vm,
            profile,
            (_, primary) =>
            {
                if (firePrimary)
                {
                    primary();
                }

                throw failure;
            }));
        Assert.AreSame(failure, observed);
        Assert.AreEqual(firePrimary ? 0 : 1, vm.Profiles.Count);
        Assert.AreEqual(firePrimary ? 1 : 0, fixture.Work.SaveCount);
        if (firePrimary)
        {
            await fixture.Work.CompleteSaveAsync(0);
        }

        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Confirmation_ActiveTargetSubmitsRealRestorationAndSerializesRemoval(bool deleteVariable)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var seed = Profile("Active");
        seed.Variables.Add(new Variable("TMP", "override", VariablesSetType.Profile));
        seed.IsEnabled = true;
        files.Files[service.ProfilesJsonFilePath] = JsonSerializer.Serialize(new[] { seed });
        fixture.Environment.User["TMP"] = "override";
        var vm = fixture.CreateViewModel(service: service);
        vm.LoadEnvironmentVariables();
        var profile = vm.AppliedProfile;
        Assert.IsNotNull(profile);
        var variable = profile.Variables.Single();
        fixture.Environment.User["TMP_PowerToys_Active"] = "original";
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action? primary = null;
        Task Present(Func<string> unusedTitle, Action callback)
        {
            primary = callback;
            return completion.Task;
        }

        var showing = deleteVariable
        ? EnvironmentVariablesPageActions.ConfirmDeleteVariableAsync(() => vm, variable, profile, Present)
        : EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(() => vm, profile, Present);
        try
        {
            primary!();
            Assert.IsFalse(showing.IsCompleted);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            await fixture.Work.CompleteActionAsync(0);
            fixture.Work.DrainRefreshes();
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual("original", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Active"));
            var saved = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(files.Writes.Single().Contents!)!;
            Assert.AreEqual(deleteVariable ? 1 : 0, saved.Count);
            if (deleteVariable)
            {
                Assert.AreEqual(0, saved.Single().Variables.Count);
            }

            Assert.IsTrue(fixture.Work.IsQuiescent);
        }
        finally
        {
            completion.TrySetResult(true);
            await showing.WaitAsync(Timeout);
        }
    }

    [TestMethod]
    public void AddVariableCommand_ComposesRealRouteAndCleanupAndRetainsPartialCastFailure()
    {
        var profile = Profile("profile");
        var effects = new List<string>();
        string? mode = "NewVariable";
        IEnumerable<object> selected = Array.Empty<object>();
        void Cleanup() => EnvironmentVariablesPageActions.CancelAddVariable(
            () => effects.Add("name-clear"),
            () => effects.Add("value-clear"),
            () => effects.Add("detach"),
            () => effects.Add("clear"),
            () => effects.Add("attach"),
            () => effects.Add("hide"));
        void Add() => EnvironmentVariablesPageActions.AddProfileVariable(
            () =>
            {
                effects.Add("profile");
                return profile;
            },
            () =>
            {
                effects.Add("mode");
                return mode!;
            },
            () =>
            {
                effects.Add("name");
                return "NEW";
            },
            () =>
            {
                effects.Add("value");
                return "one;;two";
            },
            () =>
            {
                effects.Add("selection");
                return selected;
            },
            Cleanup);
        var actions = new EnvironmentVariablesPageActions(
            () => throw new InvalidOperationException(),
            () => null!,
            () => profile,
            () => null!,
            () => Task.CompletedTask,
            Add,
            Cleanup);

        actions.AddVariableCommand.Execute(null);

        Assert.AreEqual("one;;two", profile.Variables.Single().Values);
        var expectedNewVariableOrder = new[] { "profile", "mode", "name", "value", "name-clear", "value-clear", "detach", "clear", "attach", "hide" };
        CollectionAssert.AreEqual(expectedNewVariableOrder, effects);
        effects.Clear();
        mode = null;
        var source = new Variable("EXISTING", "raw", VariablesSetType.User);
        selected = new object[] { source, "bad type" };

        Assert.ThrowsException<InvalidCastException>(() => actions.AddVariableCommand.Execute(null));

        var expectedExistingReadOrder = new[] { "profile", "mode", "selection" };
        CollectionAssert.AreEqual(expectedExistingReadOrder, effects);
        Assert.AreEqual(2, profile.Variables.Count);
        Assert.AreNotSame(source, profile.Variables[1]);
        Assert.AreEqual(VariablesSetType.Profile, profile.Variables[1].ParentType);
        effects.Clear();
        actions.CancelAddVariableCommand.Execute(null);
        var expectedExistingCleanupOrder = new[] { "name-clear", "value-clear", "detach", "clear", "attach", "hide" };
        CollectionAssert.AreEqual(expectedExistingCleanupOrder, effects);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("NewVariable")]
    public void AddRoutes_NullProfileAndRejectedNewStillCleanUp(string? mode)
    {
        var profile = mode == null ? null : Profile("profile");
        var effects = new List<string>();
        EnvironmentVariablesPageActions.AddProfileVariable(
            () =>
            {
                effects.Add("profile");
                return profile!;
            },
            () =>
            {
                effects.Add("mode");
                return mode!;
            },
            () =>
            {
                effects.Add("name");
                return "invalid=name";
            },
            () =>
            {
                effects.Add("value");
                return "raw";
            },
            () => throw new InvalidOperationException("selection"),
            () => effects.Add("cleanup"));

        var expectedReadAndCleanupOrder = mode == null ? new[] { "profile", "cleanup" } : new[] { "profile", "mode", "name", "value", "cleanup" };
        CollectionAssert.AreEqual(expectedReadAndCleanupOrder, effects);
        Assert.AreEqual(0, profile?.Variables.Count ?? 0);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void CancelFailure_StopsAtThrowAndDoesNotRepairAttachment(int throwAt)
    {
        var effects = new List<int>();
        var attached = true;
        void Step(int index)
        {
            effects.Add(index);
            if (index == throwAt)
            {
                throw new InvalidOperationException("effect");
            }

            if (index == 2)
            {
                attached = false;
            }
            else if (index == 4)
            {
                attached = true;
            }
        }

        Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariablesPageActions.CancelAddVariable(
            () => Step(0),
            () => Step(1),
            () => Step(2),
            () => Step(3),
            () => Step(4),
            () => Step(5)));

        CollectionAssert.AreEqual(Enumerable.Range(0, throwAt + 1).ToArray(), effects);
        Assert.AreEqual(throwAt < 3 || throwAt == 5, attached);
    }

    [TestMethod]
    public void FlyoutClosed_AllowsBoundedHideReentryBeforeEachDisable()
    {
        var effects = new List<string>();
        var reentered = false;
        void Cancel() => EnvironmentVariablesPageActions.CancelAddVariable(
            () => effects.Add("name"),
            () => effects.Add("value"),
            () => effects.Add("detach"),
            () => effects.Add("clear"),
            () => effects.Add("attach"),
            () =>
            {
                effects.Add("hide");
                if (!reentered)
                {
                    reentered = true;
                    EnvironmentVariablesPageActions.FlyoutClosed(Cancel, () => effects.Add("disable"));
                }
            });

        EnvironmentVariablesPageActions.FlyoutClosed(Cancel, () => effects.Add("disable"));

        var expectedReentrantCancelOrder = new[] { "name", "value", "detach", "clear", "attach", "hide", "name", "value", "detach", "clear", "attach", "hide", "disable", "disable" };
        CollectionAssert.AreEqual(expectedReentrantCancelOrder, effects);
    }

    [TestMethod]
    public void Cancel_TextClearCallbacksRunBeforeDetachAndCancelFaultPreventsDisable()
    {
        var effects = new List<string>();
        var attached = true;
        EnvironmentVariablesPageActions.CancelAddVariable(
            () =>
            {
                Assert.IsTrue(attached);
                effects.Add("name");
                effects.Add("gate");
            },
            () =>
            {
                Assert.IsTrue(attached);
                effects.Add("value");
                effects.Add("gate");
            },
            () =>
            {
                attached = false;
                effects.Add("detach");
            },
            () =>
            {
                Assert.IsFalse(attached);
                effects.Add("clear");
            },
            () =>
            {
                attached = true;
                effects.Add("attach");
            },
            () =>
            {
                Assert.IsTrue(attached);
                effects.Add("hide");
            });
        var expectedCancelOrder = new[] { "name", "gate", "value", "gate", "detach", "clear", "attach", "hide" };
        CollectionAssert.AreEqual(expectedCancelOrder, effects);

        Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.FlyoutClosed(
            () => throw new InvalidOperationException("cancel"),
            () => Assert.Fail("disable after failed cancel")));
    }

    [TestMethod]
    public void SelectionChanged_RemovesOnlySecondPerGroupThenReadsRemovalCountAndFirstEquivalent()
    {
        var profile = Profile("profile");
        var first = new Variable("A", "raw", VariablesSetType.Profile);
        var equivalent = first.Clone();
        var caseMismatch = new Variable("A", "RAW", VariablesSetType.Profile);
        profile.Variables.Add(first);
        profile.Variables.Add(equivalent);
        profile.Variables.Add(caseMismatch);
        var a1 = new Variable("A", "raw", VariablesSetType.User);
        var a2 = new Variable("a", "two", VariablesSetType.System);
        var a3 = new Variable("A", "three", VariablesSetType.User);
        var b1 = new Variable("B", "one", VariablesSetType.User);
        var b2 = new Variable("b", "two", VariablesSetType.System);
        var selected = new List<Variable> { a1, a2, a3, b1, b2 };
        var effects = new List<string>();
        var removedCount = 0;
        EnvironmentVariablesPageActions.SelectionChanged(
            profile,
            1,
            () =>
            {
                effects.Add("selected");
                return selected;
            },
            () =>
            {
                effects.Add("removed-first");
                return a1;
            },
            () =>
            {
                effects.Add("removed-count");
                return removedCount;
            },
            () => effects.Add("detach"),
            item =>
            {
                effects.Add("remove:" + item.Values);
                selected.Remove(item);
                removedCount++;
            },
            () => effects.Add("attach"),
            () => effects.Add("gate"));

        var expectedRemovalOrder = new[] { "selected", "detach", "remove:two", "attach", "detach", "remove:two", "attach", "removed-count", "removed-first", "gate" };
        CollectionAssert.AreEqual(expectedRemovalOrder, effects);
        CollectionAssert.AreEqual(new[] { a1, a3, b1 }, selected);
        CollectionAssert.AreEqual(new[] { equivalent, caseMismatch }, profile.Variables);
    }

    [TestMethod]
    public void LoadSelection_CalculatesEagerlyBeforePerItemSuppression()
    {
        var profile = Profile("profile");
        profile.Variables.Add(new Variable("A", "raw", VariablesSetType.Profile));
        profile.Variables.Add(new Variable("B", "other", VariablesSetType.Profile));
        var a = new Variable("a", "raw", VariablesSetType.User);
        var b = new Variable("B", "other", VariablesSetType.System);
        var effects = new List<string>();
        IEnumerable<Variable> Items(bool fail)
        {
            effects.Add("enumerate-a");
            yield return a;
            effects.Add("enumerate-b");
            if (fail)
            {
                throw new InvalidOperationException("late enumeration");
            }

            yield return b;
            effects.Add("enumeration-end");
        }

        void Load(bool fail) => EnvironmentVariablesPageActions.LoadSelection(
            profile,
            Items(fail),
            Array.Empty<Variable>(),
            () => effects.Add("detach"),
            item => effects.Add("add:" + item.Name),
            () => effects.Add("attach"));
        Assert.ThrowsException<InvalidOperationException>(() => Load(true));
        var expectedPendingEnumerationOrder = new[] { "enumerate-a", "enumerate-b" };
        CollectionAssert.AreEqual(expectedPendingEnumerationOrder, effects);
        effects.Clear();

        Load(false);

        var expectedCompletedEnumerationOrder = new[] { "enumerate-a", "enumerate-b", "enumeration-end", "detach", "add:a", "attach", "detach", "add:B", "attach" };
        CollectionAssert.AreEqual(expectedCompletedEnumerationOrder, effects);
    }

    [TestMethod]
    public void LoadSelection_AddFailureLeavesDetachedAndDoesNotPublishLaterItems()
    {
        var profile = Profile("profile");
        var a = new Variable("A", "raw", VariablesSetType.Profile);
        var b = new Variable("B", "other", VariablesSetType.Profile);
        profile.Variables.Add(a);
        profile.Variables.Add(b);
        var attached = true;
        var effects = new List<string>();

        Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.LoadSelection(
            profile,
            new[] { a, b },
            Array.Empty<Variable>(),
            () =>
            {
                attached = false;
                effects.Add("detach");
            },
            item =>
            {
                effects.Add(item.Name);
                throw new InvalidOperationException("add");
            },
            () =>
            {
                attached = true;
                effects.Add("attach");
            }));

        var expectedFailedAddOrder = new[] { "detach", "A" };
        CollectionAssert.AreEqual(expectedFailedAddOrder, effects);
        Assert.IsFalse(attached);
    }

    [TestMethod]
    public void SelectionChanged_EagerFailurePrecedesDetachAndRemovalFailureLeavesDetached()
    {
        var effects = new List<string>();
        var attached = true;
        IEnumerable<Variable> BadSelection()
        {
            yield return new Variable("A", "one", VariablesSetType.User);
            throw new InvalidOperationException("enumeration");
        }

        Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.SelectionChanged(
            Profile("target"),
            1,
            BadSelection,
            () => throw new InvalidOperationException("first"),
            () =>
            {
                effects.Add("count");
                return 0;
            },
            () =>
            {
                effects.Add("detach");
                attached = false;
            },
            _ => effects.Add("remove"),
            () =>
            {
                effects.Add("attach");
                attached = true;
            },
            () => effects.Add("gate")));
        Assert.AreEqual(0, effects.Count);
        Assert.IsTrue(attached);
        var duplicate = new[]
        {
        new Variable("A", "one", VariablesSetType.User),
        new Variable("a", "two", VariablesSetType.System),
        };
        Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.SelectionChanged(
            Profile("target"),
            1,
            () => duplicate,
            () => null!,
            () =>
            {
                effects.Add("count");
                return 0;
            },
            () =>
            {
                effects.Add("detach");
                attached = false;
            },
            _ =>
            {
                effects.Add("remove");
                throw new InvalidOperationException("remove");
            },
            () =>
            {
                effects.Add("attach");
                attached = true;
            },
            () => effects.Add("gate")));
        var expectedFailedRemoveOrder = new[] { "detach", "remove" };
        CollectionAssert.AreEqual(expectedFailedRemoveOrder, effects);
        Assert.IsFalse(attached);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectionChanged_NullProfileOnlyFailsWhenRemovalIsReached(bool removed)
    {
        var effects = new List<string>();
        void Change() => EnvironmentVariablesPageActions.SelectionChanged(
            null!,
            0,
            () => throw new InvalidOperationException("selected"),
            () =>
            {
                effects.Add("first");
                return new object();
            },
            () =>
            {
                effects.Add("count");
                return removed ? 1 : 0;
            },
            () => Assert.Fail("detach"),
            _ => Assert.Fail("remove"),
            () => Assert.Fail("attach"),
            () => effects.Add("gate"));
        if (removed)
        {
            Assert.ThrowsException<NullReferenceException>(Change);
        }
        else
        {
            Change();
        }

        var expectedRemovedListOrder = removed ? new[] { "count", "first" } : new[] { "count", "gate" };
        CollectionAssert.AreEqual(expectedRemovedListOrder, effects);
    }

    [DataTestMethod]
    [DataRow("matching", true)]
    [DataRow("value-case", false)]
    [DataRow("null", false)]
    [DataRow("wrong-type", false)]
    public void SelectionChanged_FirstRemovedUsesNameIgnoreCaseButValuesOrdinal(string kind, bool removes)
    {
        var profile = Profile("profile");
        var first = new Variable("A", "raw", VariablesSetType.Profile);
        var second = first.Clone();
        profile.Variables.Add(first);
        profile.Variables.Add(second);
        object? removed = kind switch
        {
            "matching" => new Variable("a", "raw", VariablesSetType.User),
            "value-case" => new Variable("a", "RAW", VariablesSetType.User),
            "wrong-type" => "not a variable",
            _ => null,
        };
        var reads = 0;
        var gates = 0;

        EnvironmentVariablesPageActions.SelectionChanged(
            profile,
            0,
            () => throw new InvalidOperationException("selected"),
            () =>
            {
                reads++;
                return removed!;
            },
            () => 2,
            () => Assert.Fail("detach"),
            _ => Assert.Fail("remove selection"),
            () => Assert.Fail("attach"),
            () => gates++);

        Assert.AreEqual(1, reads);
        Assert.AreEqual(1, gates);
        Assert.AreEqual(removes ? 1 : 2, profile.Variables.Count);
        Assert.AreSame(removes ? second : first, profile.Variables[0]);
    }

    [TestMethod]
    public void LoadSelection_MaterializesExistingBeforeItemsAndAddsOnePerNameWithOrdinalValues()
    {
        var profile = Profile("profile");
        profile.Variables.Add(new Variable("A", "raw", VariablesSetType.Profile));
        profile.Variables.Add(new Variable("B", "other", VariablesSetType.Profile));
        var a = new Variable("a", "raw", VariablesSetType.User);
        var aSecond = new Variable("A", "raw", VariablesSetType.System);
        var bWrongCase = new Variable("B", "OTHER", VariablesSetType.User);
        var b = new Variable("b", "other", VariablesSetType.System);
        var effects = new List<string>();
        var additions = new List<Variable>();
        IEnumerable<Variable> Existing()
        {
            effects.Add("selected");
            yield return new Variable("UNRELATED", "raw", VariablesSetType.User);
            effects.Add("selected-end");
        }

        IEnumerable<Variable> Items()
        {
            effects.Add("items");
            yield return a;
            yield return aSecond;
            yield return bWrongCase;
            yield return b;
            effects.Add("items-end");
        }

        EnvironmentVariablesPageActions.LoadSelection(
            profile,
            Items(),
            Existing(),
            () => effects.Add("detach"),
            item =>
            {
                additions.Add(item);
                effects.Add("add");
            },
            () => effects.Add("attach"));

        CollectionAssert.AreEqual(new[] { a, b }, additions);
        var expectedMaterializationOrder = new[] { "selected", "selected-end", "items", "items-end", "detach", "add", "attach", "detach", "add", "attach" };
        CollectionAssert.AreEqual(expectedMaterializationOrder, effects);
    }

    [TestMethod]
    public void AttachedText_TwoLiveReadsSurroundValuesObserverAndGateRunsLast()
    {
        var draft = new Variable("PATH", "old", VariablesSetType.Profile);
        var text = "one;two";
        var effects = new List<string>();
        PropertyChangedEventHandler observer = (_, args) =>
        {
            if (args.PropertyName == nameof(Variable.Values))
            {
                effects.Add("values");
                text = "three;;four;";
            }
        };
        draft.PropertyChanged += observer;
        try
        {
            EnvironmentVariablesPageActions.EditValueTextChanged(
                () =>
                {
                    effects.Add("draft");
                    return draft;
                },
                () =>
                {
                    effects.Add("text:" + text);
                    return text;
                },
                () =>
                {
                    effects.Add("gate");
                    Assert.AreEqual("one;two", draft.Values);
                    CollectionAssert.AreEqual(new[] { "three", string.Empty, "four", string.Empty }, draft.ValuesList.Select(x => x.Text).ToArray());
                });
        }
        finally
        {
            draft.PropertyChanged -= observer;
        }

        var expectedLiveReadOrder = new[] { "draft", "text:one;two", "values", "text:three;;four;", "gate" };
        CollectionAssert.AreEqual(expectedLiveReadOrder, effects);
    }

    [TestMethod]
    public void AttachedText_NullReceiverStillReadsFirstRhsAndSecondReadFailureKeepsOldList()
    {
        var effects = new List<string>();
        Assert.ThrowsException<NullReferenceException>(() => EnvironmentVariablesPageActions.EditValueTextChanged(
            () =>
            {
                effects.Add("draft");
                return null!;
            },
            () =>
            {
                effects.Add("text");
                return "new";
            },
            () => effects.Add("gate")));
        var expectedNullDraftReadOrder = new[] { "draft", "text" };
        CollectionAssert.AreEqual(expectedNullDraftReadOrder, effects);
        var draft = new Variable("PATH", "old", VariablesSetType.Profile);
        var rows = draft.ValuesList;
        var reads = 0;
        Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariablesPageActions.EditValueTextChanged(
            () => draft,
            () => ++reads == 1 ? "new" : throw new InvalidOperationException("second"),
            () => Assert.Fail("gate after failure")));
        Assert.AreEqual("new", draft.Values);
        Assert.AreSame(rows, draft.ValuesList);
    }

    [DataTestMethod]
    [DataRow("first-read")]
    [DataRow("second-null")]
    [DataRow("observer")]
    public void AttachedText_FailureShortCircuitsLaterReadsListAndGate(string failureAt)
    {
        var draft = new Variable("PATH", "old", VariablesSetType.Profile);
        var rows = draft.ValuesList;
        var reads = 0;
        var gates = 0;
        var failure = new InvalidOperationException(failureAt);
        PropertyChangedEventHandler observer = (_, args) =>
        {
            if (failureAt == "observer" && args.PropertyName == nameof(Variable.Values))
            {
                throw failure;
            }
        };
        draft.PropertyChanged += observer;
        void Change() => EnvironmentVariablesPageActions.EditValueTextChanged(
            () => draft,
            () =>
            {
                reads++;
                if (failureAt == "first-read")
                {
                    throw failure;
                }

                return reads == 2 ? null! : "new";
            },
            () => gates++);
        try
        {
            if (failureAt == "second-null")
            {
                Assert.ThrowsException<NullReferenceException>(Change);
            }
            else
            {
                Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(Change));
            }

            Assert.AreEqual(failureAt == "first-read" ? "old" : "new", draft.Values);
            Assert.AreSame(rows, draft.ValuesList);
            Assert.AreEqual(failureAt == "second-null" ? 2 : 1, reads);
            Assert.AreEqual(0, gates);
        }
        finally
        {
            draft.PropertyChanged -= observer;
        }
    }

    [TestMethod]
    public async Task AttachedText_StableRawValueReachesRealEditCommandAndSerializer()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var vm = fixture.CreateViewModel(service: service);
        var profile = Profile("profile");
        var original = new Variable("PATH", "old", VariablesSetType.Profile);
        profile.Variables.Add(original);
        vm.Profiles = new() { profile };
        var draft = original.Clone();
        var actions = new EnvironmentVariablesPageActions(
            () => vm,
            () => draft,
            () => profile,
            () => null!,
            () => Task.CompletedTask,
            () =>
            {
            },
            () =>
            {
            });
        var command = actions.EditCommand;
        var gates = 0;

        EnvironmentVariablesPageActions.EditValueTextChanged(() => draft, () => "new1;;new2;", () => gates++);
        command.Execute(new EnvironmentVariablesPageActions.EditTarget(original, profile));

        Assert.AreEqual(1, gates);
        Assert.AreEqual("new1;;new2;", original.Values);
        CollectionAssert.AreEqual(new[] { "new1", string.Empty, "new2", string.Empty }, original.ValuesList.Select(x => x.Text).ToArray());
        await fixture.Work.CompleteActionAsync(0);
        fixture.Work.DrainRefreshes();
        await fixture.Work.CompleteSaveAsync(0);
        var saved = JsonSerializer.Deserialize<List<ProfileVariablesSet>>(files.Writes.Single().Contents!)!.Single();
        Assert.AreEqual("new1;;new2;", saved.Variables.Single().Values);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [DataTestMethod]
    [DataRow("before", false)]
    [DataRow("before", true)]
    [DataRow("after", false)]
    [DataRow("after", true)]
    [DataRow("commit", false)]
    [DataRow("commit", true)]
    public void SuppressedPublisher_TransportsJoinedTextWithoutBindingEchoOrFinally(string operation, bool fail)
    {
        var draft = new Variable("PATH", "one;two", VariablesSetType.Profile);
        var joined = operation switch
        {
            "before" => VariableEditorState.InsertBefore(draft, draft.ValuesList[1]),
            "after" => VariableEditorState.InsertAfter(draft, draft.ValuesList[0]),
            _ => VariableEditorState.CommitEntryText(draft, draft.ValuesList[1], "two2"),
        };
        var effects = new List<string>();
        var attached = true;
        string? published = null;
        void Publish() => EnvironmentVariablesPageActions.SetRawTextWithHandlerSuppressed(
            joined!,
            () =>
            {
                effects.Add("detach");
                attached = false;
            },
            text =>
            {
                effects.Add("text");
                Assert.IsFalse(attached);
                published = text;
                if (fail)
                {
                    throw new InvalidOperationException("sink");
                }
            },
            () =>
            {
                effects.Add("attach");
                attached = true;
            });
        if (fail)
        {
            Assert.ThrowsException<InvalidOperationException>(Publish);
        }
        else
        {
            Publish();
        }

        var expectedDetachTextOrder = fail ? new[] { "detach", "text" } : new[] { "detach", "text", "attach" };
        CollectionAssert.AreEqual(expectedDetachTextOrder, effects);
        Assert.AreEqual(!fail, attached);
        Assert.AreEqual(operation == "commit" ? "one;two2" : "one;;two", published);
        Assert.AreEqual("one;two", draft.Values);
        var expectedValueItems = operation == "commit" ? new[] { "one", "two2" } : new[] { "one", string.Empty, "two" };
        CollectionAssert.AreEqual(expectedValueItems, draft.ValuesList.Select(x => x.Text).ToArray());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Reload_ResetsAfterNormalReturnIncludingCaughtProfileReadFailure(bool caughtFailure)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
        fixture.Service.Read = () => caughtFailure ? throw new InvalidOperationException("profiles") : new();

        EnvironmentVariablesPageActions.Reload(vm);

        Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
        Assert.IsFalse(vm.IsInfoBarButtonVisible);
        Assert.AreEqual(0, vm.Profiles.Count);
        Assert.AreEqual(caughtFailure ? 1 : 0, fixture.Logger.Entries.Count);
        if (caughtFailure)
        {
            Assert.AreEqual("Failed to load profiles.json file", fixture.Logger.Entries.Single().Message);
        }

        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [TestMethod]
    public void ReloadOuterFailure_RetainsWarningAndDismissDoesNotLoad()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
        var serviceReads = 0;
        fixture.Service.Read = () =>
        {
            serviceReads++;
            return new();
        };
        fixture.Environment.BeforeOperation = (operation, hive, _) =>
        {
            if (operation == "Open" && hive == RegistryHive.LocalMachine)
            {
                throw new InvalidOperationException("defaults");
            }
        };

        Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariablesPageActions.Reload(vm));

        Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
        Assert.AreEqual(0, serviceReads);
        var opens = fixture.Environment.Opens.Count;
        EnvironmentVariablesPageActions.DismissWarning(vm);
        Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
        Assert.IsFalse(vm.IsInfoBarButtonVisible);
        Assert.AreEqual(opens, fixture.Environment.Opens.Count);
        Assert.AreEqual(0, serviceReads);
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReloadRejectedProfile_PreservesUnapplyWorkAndOnlyResetsAfterFinalReadReturns(bool failFinalRead)
    {
        using var fixture = new EnvironmentVariablesFixture();
        fixture.Environment.User["TMP"] = "override";
        var old = Profile("Old");
        old.Variables.Add(new Variable("TMP", "override", VariablesSetType.Profile));
        old.IsEnabled = true;
        fixture.Service.Read = () => new() { old };
        var vm = fixture.CreateViewModel();
        vm.LoadEnvironmentVariables();
        Assert.AreSame(old, vm.AppliedProfile);
        fixture.Environment.User["TMP_PowerToys_Old"] = "original";
        var rejected = Profile(string.Empty);
        rejected.IsEnabled = true;
        fixture.Service.Read = () => new() { rejected };
        vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
        var machineReads = 0;
        fixture.Environment.BeforeOperation = (operation, hive, _) =>
        {
            if (operation == "Open" && hive == RegistryHive.LocalMachine && ++machineReads == 2 && failFinalRead)
            {
                throw new InvalidOperationException("final default read");
            }
        };
        if (failFinalRead)
        {
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariablesPageActions.Reload(vm));
        }
        else
        {
            EnvironmentVariablesPageActions.Reload(vm);
        }

        fixture.Environment.BeforeOperation = null;
        Assert.AreEqual(failFinalRead ? EnvironmentState.ProfileNameInvalid : EnvironmentState.Unchanged, vm.EnvironmentState);
        Assert.IsNull(vm.AppliedProfile);
        Assert.IsFalse(old.IsEnabled);
        Assert.IsFalse(rejected.IsEnabled);
        Assert.AreEqual(1, fixture.Work.ActionCount);
        Assert.AreEqual(1, fixture.Work.SaveCount);
        Assert.AreEqual("override", fixture.Environment.User["TMP"]);
        await fixture.Work.CompleteActionAsync(0);
        fixture.Work.DrainRefreshes();
        await fixture.Work.CompleteSaveAsync(0);
        Assert.AreEqual("original", fixture.Environment.User["TMP"]);
        Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [DataTestMethod]
    [DataRow("new", "select")]
    [DataRow("new", "configure")]
    [DataRow("new", "publish")]
    [DataRow("new", "show")]
    [DataRow("edit", "select")]
    [DataRow("edit", "target")]
    [DataRow("edit", "configure")]
    [DataRow("edit", "clone")]
    [DataRow("edit", "publish")]
    [DataRow("edit", "show")]
    public async Task ProfileDialog_FaultsStopAtExactEffectWithoutRollback(string route, string failingEffect)
    {
        var target = Profile("original");
        target.Variables.Add(new Variable("A", "old", VariablesSetType.Profile));
        if (route == "edit" && (failingEffect == "clone" || failingEffect == "configure"))
        {
            target.Variables = null!;
        }

        var effects = new List<string>();
        var failure = new InvalidOperationException(failingEffect);
        ProfileVariablesSet? published = null;
        void Effect(string effect)
        {
            effects.Add(effect);
            if (effect == failingEffect)
            {
                throw failure;
            }
        }

        void Publish(ProfileVariablesSet draft)
        {
            published = draft;
            Effect("publish");
        }

        Task Show()
        {
            effects.Add("show");
            return Task.FromException(failure);
        }

        Task Invoke() => (route == "new"
        ? EnvironmentVariablesPageActions.ShowNewProfileAsync(
            () => Effect("select"),
            () => Effect("configure"),
            Publish,
            Show)
        : EnvironmentVariablesPageActions.ShowProfileEditAsync(
            () => Effect("select"),
            () =>
            {
                Effect("target");
                return target;
            },
            () => Effect("configure"),
            Publish,
            Show)).WaitAsync(Timeout);
        if (failingEffect == "clone")
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(Invoke);
        }
        else
        {
            Assert.AreSame(failure, await Assert.ThrowsExceptionAsync<InvalidOperationException>(Invoke));
        }

        var order = route == "new"
        ? new[] { "select", "configure", "publish", "show" }
        : new[] { "select", "target", "configure", "publish", "show" };
        var reached = failingEffect == "clone" ? 3 : Array.IndexOf(order, failingEffect) + 1;
        CollectionAssert.AreEqual(order.Take(reached).ToArray(), effects);
        Assert.AreEqual("original", target.Name);
        if (failingEffect == "publish" || failingEffect == "show")
        {
            Assert.IsNotNull(published);
            Assert.AreNotSame(target, published);
            if (route == "edit")
            {
                Assert.AreEqual(target.Id, published.Id);
                Assert.AreSame(target.Variables[0], published.Variables[0]);
            }
            else
            {
                Assert.AreEqual(string.Empty, published.Name);
                Assert.AreEqual(0, published.Variables.Count);
            }
        }
        else
        {
            Assert.IsNull(published);
        }
    }

    [DataTestMethod]
    [DataRow("configure")]
    [DataRow("publish")]
    [DataRow("gate")]
    [DataRow("show")]
    public async Task DefaultDialog_FaultsPreservePublicationAndStopLaterEffects(string failingEffect)
    {
        var effects = new List<string>();
        var failure = new InvalidOperationException(failingEffect);
        Variable? published = null;
        void Effect(string effect)
        {
            effects.Add(effect);
            if (effect == failingEffect)
            {
                throw failure;
            }
        }

        var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.ShowDefaultAddAsync(
            new DefaultVariablesSet(VariablesSet.SystemGuid, "System", VariablesSetType.System),
            () => Effect("configure"),
            draft =>
            {
                published = draft;
                Effect("publish");
            },
            () => Effect("gate"),
            () =>
            {
                effects.Add("show");
                return Task.FromException(failure);
            }).WaitAsync(Timeout));

        Assert.AreSame(failure, observed);
        var order = new[] { "configure", "publish", "gate", "show" };
        CollectionAssert.AreEqual(order.Take(Array.IndexOf(order, failingEffect) + 1).ToArray(), effects);
        if (failingEffect == "configure")
        {
            Assert.IsNull(published);
        }
        else
        {
            Assert.IsNotNull(published);
            Assert.AreEqual(VariablesSetType.System, published.ParentType);
            Assert.AreEqual(string.Empty, published.Name);
            Assert.AreEqual(string.Empty, published.Values);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DialogNullTargets_ProfileSkipsConfigureButDefaultConfigureFailureWins(bool defaultDialog)
    {
        var effects = new List<string>();
        var failure = new InvalidOperationException("configure before null scope");
        void Configure()
        {
            effects.Add("configure");
            throw failure;
        }

        if (defaultDialog)
        {
            var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            EnvironmentVariablesPageActions.ShowDefaultAddAsync(
                null!,
                Configure,
                _ => effects.Add("publish"),
                () => effects.Add("gate"),
                () =>
                {
                    effects.Add("show");
                    return Task.CompletedTask;
                }).WaitAsync(Timeout));
            Assert.AreSame(failure, observed);
            var expectedDefaultConfigurationOrder = new[] { "configure" };
            CollectionAssert.AreEqual(expectedDefaultConfigurationOrder, effects);
        }
        else
        {
            await EnvironmentVariablesPageActions.ShowProfileEditAsync(
                () => effects.Add("select"),
                () =>
                {
                    effects.Add("target");
                    return null!;
                },
                Configure,
                _ => effects.Add("publish"),
                () =>
                {
                    effects.Add("show");
                    return Task.CompletedTask;
                }).WaitAsync(Timeout);
            var expectedProfileSelectionOrder = new[] { "select", "target" };
            CollectionAssert.AreEqual(expectedProfileSelectionOrder, effects);
        }
    }

    [DataTestMethod]
    [DataRow("NewVariable", "profile")]
    [DataRow("NewVariable", "mode")]
    [DataRow("NewVariable", "name")]
    [DataRow("NewVariable", "value")]
    [DataRow("unexpected-mode", "profile")]
    [DataRow("unexpected-mode", "mode")]
    [DataRow("unexpected-mode", "selection")]
    public void AddRoute_GetterFaultsStopLaterReadsAndSkipCleanup(string mode, string failingRead)
    {
        var profile = Profile("profile");
        var effects = new List<string>();
        var failure = new InvalidOperationException(failingRead);
        void Read(string name)
        {
            effects.Add(name);
            if (name == failingRead)
            {
                throw failure;
            }
        }

        var observed = Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.AddProfileVariable(
            () =>
            {
                Read("profile");
                return profile;
            },
            () =>
            {
                Read("mode");
                return mode;
            },
            () =>
            {
                Read("name");
                return "NEW";
            },
            () =>
            {
                Read("value");
                return "raw";
            },
            () =>
            {
                Read("selection");
                return Array.Empty<object>();
            },
            () => effects.Add("cleanup")));

        Assert.AreSame(failure, observed);
        var order = mode == "NewVariable"
        ? new[] { "profile", "mode", "name", "value" }
        : new[] { "profile", "mode", "selection" };
        CollectionAssert.AreEqual(order.Take(Array.IndexOf(order, failingRead) + 1).ToArray(), effects);
        Assert.AreEqual(0, profile.Variables.Count);
    }

    [DataTestMethod]
    [DataRow("duplicate")]
    [DataRow("null-value")]
    [DataRow("duplicate-null-value")]
    public void AddNewRoute_DuplicateCleansUpButNullValueFailsBeforeDuplicateCheck(string input)
    {
        var profile = Profile("profile");
        var original = new Variable("Alpha", "old", VariablesSetType.Profile);
        profile.Variables.Add(original);
        var effects = new List<string>();
        var name = input == "null-value" ? "Beta" : "aLPHA";
        string? value = input == "duplicate" ? "new" : null;
        void Cleanup()
        {
            effects.Add("cleanup");
            EnvironmentVariablesPageActions.CancelAddVariable(
                () => effects.Add("clear-name"),
                () => effects.Add("clear-value"),
                () => effects.Add("detach"),
                () => effects.Add("clear-selection"),
                () => effects.Add("attach"),
                () => effects.Add("hide"));
        }

        void Add() => EnvironmentVariablesPageActions.AddProfileVariable(
            () =>
            {
                effects.Add("profile");
                return profile;
            },
            () =>
            {
                effects.Add("mode");
                return "NewVariable";
            },
            () =>
            {
                effects.Add("name");
                return name;
            },
            () =>
            {
                effects.Add("value");
                return value!;
            },
            () => throw new InvalidOperationException("New route read selection"),
            Cleanup);
        if (value == null)
        {
            Assert.ThrowsException<NullReferenceException>(Add);
            var expectedPendingReadOrder = new[] { "profile", "mode", "name", "value" };
            CollectionAssert.AreEqual(expectedPendingReadOrder, effects);
        }
        else
        {
            Add();
            var expectedCompletedCleanupOrder = new[] { "profile", "mode", "name", "value", "cleanup", "clear-name", "clear-value", "detach", "clear-selection", "attach", "hide" };
            CollectionAssert.AreEqual(expectedCompletedCleanupOrder, effects);
        }

        Assert.AreSame(original, profile.Variables.Single());
        Assert.AreEqual("old", original.Values);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddExistingRoute_UnknownModeEnumeratesLazilyAndOnlyNormalReturnCleansUp(bool failAfterFirst)
    {
        var profile = Profile("profile");
        var first = new Variable("FIRST", "one;;two", VariablesSetType.User);
        var second = new Variable("SECOND", "raw", VariablesSetType.System);
        var effects = new List<string>();
        var failure = new InvalidOperationException("late selection enumeration");
        IEnumerable<object> Selection()
        {
            effects.Add("enumerate-first");
            yield return first;
            effects.Add("enumerate-second");
            Assert.AreEqual(1, profile.Variables.Count);
            Assert.AreNotSame(first, profile.Variables[0]);
            Assert.AreEqual("FIRST", profile.Variables[0].Name);
            if (failAfterFirst)
            {
                throw failure;
            }

            yield return second;
            effects.Add("enumeration-end");
        }

        void Cleanup()
        {
            effects.Add("cleanup");
            EnvironmentVariablesPageActions.CancelAddVariable(
                () => effects.Add("clear-name"),
                () => effects.Add("clear-value"),
                () => effects.Add("detach"),
                () => effects.Add("clear-selection"),
                () => effects.Add("attach"),
                () => effects.Add("hide"));
        }

        void Add() => EnvironmentVariablesPageActions.AddProfileVariable(
            () =>
            {
                effects.Add("profile");
                return profile;
            },
            () =>
            {
                effects.Add("mode");
                return "unexpected-mode";
            },
            () => throw new InvalidOperationException("Existing route read name"),
            () => throw new InvalidOperationException("Existing route read value"),
            () =>
            {
                effects.Add("selection");
                return Selection();
            },
            Cleanup);
        if (failAfterFirst)
        {
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(Add));
            var expectedPendingEnumerationOrder = new[] { "profile", "mode", "selection", "enumerate-first", "enumerate-second" };
            CollectionAssert.AreEqual(expectedPendingEnumerationOrder, effects);
        }
        else
        {
            Add();
            var expectedCompletedEnumerationOrder = new[] { "profile", "mode", "selection", "enumerate-first", "enumerate-second", "enumeration-end", "cleanup", "clear-name", "clear-value", "detach", "clear-selection", "attach", "hide" };
            CollectionAssert.AreEqual(expectedCompletedEnumerationOrder, effects);
        }

        Assert.AreEqual(failAfterFirst ? 1 : 2, profile.Variables.Count);
        Assert.AreEqual("one;;two", profile.Variables[0].Values);
        Assert.IsTrue(profile.Variables.All(item => item.ParentType == VariablesSetType.Profile));
        if (!failAfterFirst)
        {
            Assert.AreNotSame(second, profile.Variables[1]);
            Assert.AreEqual("SECOND", profile.Variables[1].Name);
        }
    }

    [DataTestMethod]
    [DataRow("count")]
    [DataRow("first")]
    [DataRow("gate")]
    public void SelectionChanged_LateFaultsRetainPriorSelectionAndProfileEffects(string failingEffect)
    {
        var profile = Profile("profile");
        var first = new Variable("A", "raw", VariablesSetType.Profile);
        var equivalent = first.Clone();
        profile.Variables.Add(first);
        profile.Variables.Add(equivalent);
        var a1 = new Variable("A", "raw", VariablesSetType.User);
        var a2 = new Variable("a", "second", VariablesSetType.System);
        var b1 = new Variable("B", "raw", VariablesSetType.User);
        var b2 = new Variable("b", "second", VariablesSetType.System);
        var selected = new List<Variable> { a1, a2, b1, b2 };
        var effects = new List<string>();
        var attached = true;
        var failure = new InvalidOperationException(failingEffect);
        void Effect(string effect)
        {
            effects.Add(effect);
            if (effect == failingEffect)
            {
                throw failure;
            }
        }

        var observed = Assert.ThrowsException<InvalidOperationException>(() =>
        EnvironmentVariablesPageActions.SelectionChanged(
            profile,
            1,
            () =>
            {
                Effect("selected");
                return selected;
            },
            () =>
            {
                Effect("first");
                return a1;
            },
            () =>
            {
                Effect("count");
                return 1;
            },
            () =>
            {
                attached = false;
                Effect("detach");
            },
            item =>
            {
                Effect("remove:" + item.Name);
                selected.Remove(item);
            },
            () =>
            {
                attached = true;
                Effect("attach");
            },
            () => Effect("gate")));

        Assert.AreSame(failure, observed);
        var order = new[] { "selected", "detach", "remove:a", "attach", "detach", "remove:b", "attach", "count", "first", "gate" };
        CollectionAssert.AreEqual(order.Take(Array.IndexOf(order, failingEffect) + 1).ToArray(), effects);
        CollectionAssert.AreEqual(new[] { a1, b1 }, selected);
        Assert.IsTrue(attached);
        Assert.AreEqual(failingEffect == "gate" ? 1 : 2, profile.Variables.Count);
        Assert.AreSame(failingEffect == "gate" ? equivalent : first, profile.Variables[0]);
    }

    [DataTestMethod]
    [DataRow("null-enumerable")]
    [DataRow("wrong-type")]
    [DataRow("enumeration-failure")]
    [DataRow("null-member")]
    public void LoadSelection_MalformedExistingInputFailsEagerlyWithoutPublication(string malformed)
    {
        var profile = Profile("profile");
        profile.Variables.Add(new Variable("A", "raw", VariablesSetType.Profile));
        var effects = new List<string>();
        var failure = new InvalidOperationException("existing selection");
        var attached = true;
        var published = new List<Variable>();
        IEnumerable<Variable> ThrowingSelection()
        {
            effects.Add("selected-first");
            yield return new Variable("OTHER", "raw", VariablesSetType.User);
            effects.Add("selected-failure");
            throw failure;
        }

        IEnumerable<Variable> Items()
        {
            effects.Add("items-unrelated");
            yield return new Variable("UNRELATED", "raw", VariablesSetType.User);
            effects.Add("items-matching");
            yield return new Variable("a", "raw", VariablesSetType.User);
            effects.Add("items-end");
        }

        IEnumerable<Variable> selected = malformed switch
        {
            "null-enumerable" => null!,
            "wrong-type" => new object[] { new Variable("OTHER", "raw", VariablesSetType.User), "bad selection" }.Cast<Variable>(),
            "enumeration-failure" => ThrowingSelection(),
            _ => new Variable[] { new Variable("OTHER", "raw", VariablesSetType.User), null! },
        };
        void Load() => EnvironmentVariablesPageActions.LoadSelection(
            profile,
            Items(),
            selected,
            () =>
            {
                attached = false;
                effects.Add("detach");
            },
            item =>
            {
                published.Add(item);
                effects.Add("add");
            },
            () =>
            {
                attached = true;
                effects.Add("attach");
            });
        switch (malformed)
        {
            case "null-enumerable":
            Assert.ThrowsException<ArgumentNullException>(Load);
            break;
            case "wrong-type":
            Assert.ThrowsException<InvalidCastException>(Load);
            break;
            case "enumeration-failure":
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(Load));
            break;
            default:
            Assert.ThrowsException<NullReferenceException>(Load);
            break;
        }

        var expected = malformed switch
        {
            "enumeration-failure" => new[] { "selected-first", "selected-failure" },
            "null-member" => new[] { "items-unrelated", "items-matching" },
            _ => Array.Empty<string>(),
        };
        CollectionAssert.AreEqual(expected, effects);
        Assert.IsTrue(attached);
        Assert.AreEqual(0, published.Count);
        Assert.AreEqual(1, profile.Variables.Count);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TypedCommand_WrongArgumentIsRejectedBeforeAnyDomainRead(bool defaultCommand)
    {
        var reads = 0;
        object Read()
        {
            reads++;
            throw new InvalidOperationException("Unexpected domain read.");
        }

        var actions = new EnvironmentVariablesPageActions(
            () => (MainViewModel)Read(),
            Read,
            Read,
            Read,
            () =>
            {
                Read();
                return Task.CompletedTask;
            },
            () => Read(),
            () => Read());
        var command = defaultCommand ? actions.AddDefaultVariableCommand : actions.EditCommand;
        Assert.AreEqual(0, reads);

        Assert.ThrowsException<ArgumentException>(() => command.Execute(new object()));

        Assert.AreEqual(0, reads);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Confirmation_PrimaryCollectionObserverFailureRetainsMutationWithoutLaterSave(bool deleteVariable)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var vm = fixture.CreateViewModel();
        var profile = Profile("target");
        var unrelatedProfile = Profile("unrelated");
        var variable = new Variable("TARGET", "raw", VariablesSetType.Profile);
        var unrelatedVariable = new Variable("OTHER", "kept", VariablesSetType.Profile);
        profile.Variables.Add(variable);
        profile.Variables.Add(unrelatedVariable);
        vm.Profiles = new() { profile, unrelatedProfile };
        var effects = new List<string>();
        var failure = new InvalidOperationException("collection observer");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action? primary = null;
        Task Present(Func<string> unusedTitle, Action callback)
        {
            effects.Add("show");
            primary = callback;
            return completion.Task;
        }

        MainViewModel ReadVm()
        {
            effects.Add("vm");
            return vm;
        }

        INotifyCollectionChanged observedCollection = deleteVariable ? profile.Variables : vm.Profiles;
        object expectedRemoved = deleteVariable ? variable : profile;
        NotifyCollectionChangedEventHandler observer = (_, args) =>
        {
            effects.Add("observer");
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
            Assert.AreSame(expectedRemoved, args.OldItems![0]);
            throw failure;
        };
        observedCollection.CollectionChanged += observer;
        var showing = deleteVariable
        ? EnvironmentVariablesPageActions.ConfirmDeleteVariableAsync(ReadVm, variable, profile, Present)
        : EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(ReadVm, profile, Present);
        try
        {
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => primary!()));
            Assert.IsFalse(showing.IsCompleted);
            var expectedObserverFailureOrder = new[] { "show", "vm", "observer" };
            CollectionAssert.AreEqual(expectedObserverFailureOrder, effects);
            if (deleteVariable)
            {
                Assert.AreSame(unrelatedVariable, profile.Variables.Single());
                Assert.AreEqual(2, vm.Profiles.Count);
            }
            else
            {
                Assert.AreSame(unrelatedProfile, vm.Profiles.Single());
                Assert.AreEqual(2, profile.Variables.Count);
            }

            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.AreEqual(0, fixture.Environment.Opens.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }
        finally
        {
            observedCollection.CollectionChanged -= observer;
            completion.TrySetResult(true);
            await showing.WaitAsync(Timeout);
        }

        var expectedRetainedObserverOrder = new[] { "show", "vm", "observer" };
        CollectionAssert.AreEqual(expectedRetainedObserverOrder, effects);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        Assert.AreEqual(0, fixture.Work.ActionCount);
    }

    private static ProfileVariablesSet Profile(string name) => new(Guid.NewGuid(), name);
}
