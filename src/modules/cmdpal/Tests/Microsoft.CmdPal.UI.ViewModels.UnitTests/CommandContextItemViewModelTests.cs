// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class CommandContextItemViewModelTests
{
    [TestMethod]
    public async Task RequestedShortcut_ChangesAndRemovalUpdateLookupAndHints()
    {
        var pageContext = new TestPageContext();
        var initialShortcut = new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.F8, 0);
        var replacementShortcut = new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.F9, 0);
        var item = new CommandContextItem(new NoOpCommand { Name = "Action" }) { RequestedShortcut = initialShortcut };
        var command = new CommandContextItemViewModel(item, new(pageContext));
        command.InitializeProperties();
        command.SetDefaultShortcut(DefaultShortcutRole.Secondary);

        try
        {
            Assert.AreEqual(initialShortcut, command.RequestedShortcut);
            Assert.AreSame(command, IContextMenuContext.FindKeybinding([command], initialShortcut));

            await ChangeShortcutAsync(command, () => item.RequestedShortcut = replacementShortcut);

            Assert.AreEqual(replacementShortcut, command.RequestedShortcut);
            Assert.IsTrue(command.HasRequestedShortcut);
            Assert.AreEqual(replacementShortcut, command.DisplayShortcut);
            Assert.IsNull(IContextMenuContext.FindKeybinding([command], initialShortcut));
            Assert.AreSame(command, IContextMenuContext.FindKeybinding([command], replacementShortcut));

            await ChangeShortcutAsync(command, () => item.RequestedShortcut = default);

            Assert.IsFalse(command.HasRequestedShortcut);
            Assert.AreEqual(CommandContextItemViewModel.SecondaryShortcut, command.DisplayShortcut);
            Assert.IsNull(IContextMenuContext.FindKeybinding([command], replacementShortcut));
            Assert.IsNull(IContextMenuContext.FindKeybinding([command], CommandContextItemViewModel.SecondaryShortcut));
        }
        finally
        {
            command.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    [DataRow(VirtualKeyModifiers.None, 0)]
    [DataRow(VirtualKeyModifiers.Control, 0)]
    [DataRow(VirtualKeyModifiers.None, 28)]
    [DataRow(VirtualKeyModifiers.Control, 28)]
    public async Task RequestedShortcut_ChangesStillSuppressReservedEnter(VirtualKeyModifiers modifiers, int scanCode)
    {
        var pageContext = new TestPageContext();
        var item = new CommandContextItem(new NoOpCommand { Name = "Action" })
        {
            RequestedShortcut = new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.F8, 0),
        };
        var command = new CommandContextItemViewModel(item, new(pageContext));
        command.InitializeProperties();
        var reservedShortcut = new KeyChord(modifiers, (int)VirtualKey.Enter, scanCode);

        try
        {
            await ChangeShortcutAsync(command, () => item.RequestedShortcut = reservedShortcut);

            Assert.IsNull(command.RequestedShortcut);
            Assert.IsFalse(command.HasRequestedShortcut);
            Assert.IsNull(command.DisplayShortcut);
            Assert.IsNull(IContextMenuContext.FindKeybinding([command], reservedShortcut));

            var allowedShortcut = new KeyChord(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, (int)VirtualKey.Enter, 0);
            await ChangeShortcutAsync(command, () => item.RequestedShortcut = allowedShortcut);

            Assert.AreEqual(allowedShortcut, command.RequestedShortcut);
            Assert.AreEqual(allowedShortcut, command.DisplayShortcut);
            Assert.AreSame(command, IContextMenuContext.FindKeybinding([command], allowedShortcut));
        }
        finally
        {
            command.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IsCritical_ChangesUpdateCachedValueAndNotify(bool critical)
    {
        var pageContext = new TestPageContext();
        var item = new CommandContextItem(new NoOpCommand { Name = "Action" }) { IsCritical = !critical };
        var command = new CommandContextItemViewModel(item, new(pageContext));
        command.InitializeProperties();

        try
        {
            Assert.AreEqual(!critical, command.IsCritical);

            await ChangeAndWaitAsync(command, () => item.IsCritical = critical, nameof(command.IsCritical));

            Assert.AreEqual(critical, command.IsCritical);
        }
        finally
        {
            command.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    private static Task ChangeShortcutAsync(CommandContextItemViewModel command, Action change) =>
        ChangeAndWaitAsync(command, change, nameof(command.RequestedShortcut), nameof(command.HasRequestedShortcut), nameof(command.DisplayShortcut));

    private static async Task ChangeAndWaitAsync(CommandContextItemViewModel command, Action change, params string[] propertyNames)
    {
        var notifications = propertyNames.ToDictionary(name => name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        PropertyChangedEventHandler onChanged = (_, args) =>
        {
            if (args.PropertyName is not null && notifications.TryGetValue(args.PropertyName, out var notification))
            {
                notification.TrySetResult();
            }
        };
        command.PropertyChanged += onChanged;
        try
        {
            change();
            await Task.WhenAll(notifications.Values.Select(notification => notification.Task)).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            command.PropertyChanged -= onChanged;
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }
}
