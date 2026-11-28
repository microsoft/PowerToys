// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.Ext.WinGet.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.WinGet.UnitTests;

[TestClass]
public sealed class WinGetExtensionCommandsProviderTests : IDisposable
{
    private readonly List<WinGetPackageOperation> _operations = [];
    private readonly List<string> _notifications = [];
    private readonly Mock<IWinGetOperationTrackerService> _tracker = new();
    private readonly QueuedTaskScheduler _scheduler = new();
    private WinGetExtensionCommandsProvider _provider = null!;
    private IPage _page = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tracker.SetupGet(tracker => tracker.Operations).Returns(_operations);
        _provider = new WinGetExtensionCommandsProvider(Mock.Of<IWinGetPackageManagerService>(), _tracker.Object, _scheduler);
        _provider.NotificationRequested += (_, message) => _notifications.Add(message);
        _page = (IPage)_provider.TopLevelCommands()[0].Command;
    }

    public void Dispose()
    {
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationKind.Install)]
    [DataRow(WinGetPackageOperationKind.Uninstall)]
    public void StartedOperationUpdatesFooterWithoutRenamingSearchCommand(WinGetPackageOperationKind kind)
    {
        var operation = CreateOperation("Test.App", "Test App", kind);

        StartOperation(operation);

        var expectedMessage = Format(
            kind == WinGetPackageOperationKind.Uninstall ? Resources.winget_uninstalling_package : Resources.winget_installing_package,
            operation.PackageName);
        Assert.AreEqual(expectedMessage, _page.Title);
        Assert.AreEqual(Resources.winget_page_name, _page.Name);
        Assert.AreEqual(Resources.winget_page_name, _provider.TopLevelCommands()[0].Title);
        CollectionAssert.AreEqual(new[] { expectedMessage }, _notifications);
    }

    [TestMethod]
    public void DownloadProgressKeepsAppNameAndDoesNotRepeatNotification()
    {
        var operation = CreateOperation("Test.App", "Test App");
        StartOperation(operation);
        operation = operation with { State = WinGetPackageOperationState.Downloading, ProgressPercent = 50 };
        _operations[0] = operation;

        _tracker.Raise(tracker => tracker.OperationUpdated += null, new WinGetPackageOperationEventArgs(operation));
        _scheduler.RunAll();

        Assert.AreEqual(Format(Resources.winget_installing_package, operation.PackageName), _page.Title);
        Assert.HasCount(1, _notifications);
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationKind.Install)]
    [DataRow(WinGetPackageOperationKind.Uninstall)]
    public void SuccessfulOperationRestoresFooterAndNotifiesCompletion(WinGetPackageOperationKind kind)
    {
        var operation = CreateOperation("Test.App", "Test App", kind);
        StartOperation(operation);

        CompleteOperation(operation, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.AreEqual(Resources.winget_page_name, _page.Name);
        Assert.HasCount(2, _notifications);
        Assert.AreEqual(
            Format(kind == WinGetPackageOperationKind.Uninstall ? Resources.winget_uninstall_package_finished : Resources.winget_install_package_finished, operation.PackageName),
            _notifications[1]);
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationState.Failed)]
    [DataRow(WinGetPackageOperationState.Canceled)]
    public void UnsuccessfulOperationRestoresFooterWithoutSuccessNotification(WinGetPackageOperationState state)
    {
        var operation = CreateOperation("Test.App", "Test App");
        StartOperation(operation);

        CompleteOperation(operation, state);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.HasCount(2, _notifications);
        Assert.AreEqual(
            Format(state == WinGetPackageOperationState.Canceled ? Resources.winget_operation_canceled : Resources.winget_operation_failed, operation.PackageName),
            _notifications[1]);
    }

    [TestMethod]
    public void CompletingLatestOperationKeepsRemainingOperationInFooter()
    {
        var first = CreateOperation("Test.First", "First App");
        var second = CreateOperation("Test.Second", "Second App");
        var gallery = CreateOperation("Test.Gallery", "Gallery Extension", source: WinGetPackageOperationSource.Unspecified);
        StartOperation(first);
        StartOperation(gallery);
        StartOperation(second);
        Assert.AreEqual(Format(Resources.winget_installing_package, second.PackageName), _page.Title);

        CompleteOperation(second, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(Format(Resources.winget_installing_package, first.PackageName), _page.Title);

        CompleteOperation(first, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.HasCount(4, _notifications);
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationSource.Unspecified)]
    [DataRow(WinGetPackageOperationSource.WinGetExtension)]
    public void ConstructorRestoresOnlyWinGetExtensionOperationTitle(WinGetPackageOperationSource source)
    {
        var operation = CreateOperation("Test.App", "Test App", source: source);
        _operations.Add(operation);
        var provider = new WinGetExtensionCommandsProvider(Mock.Of<IWinGetPackageManagerService>(), _tracker.Object, _scheduler);
        try
        {
            var expectedTitle = source == WinGetPackageOperationSource.WinGetExtension
                ? Format(Resources.winget_installing_package, operation.PackageName)
                : string.Empty;
            Assert.AreEqual(expectedTitle, ((IPage)provider.TopLevelCommands()[0].Command).Title);
        }
        finally
        {
            provider.Dispose();
        }
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationKind.Install, WinGetPackageOperationState.Succeeded)]
    [DataRow(WinGetPackageOperationKind.Install, WinGetPackageOperationState.Failed)]
    [DataRow(WinGetPackageOperationKind.Install, WinGetPackageOperationState.Canceled)]
    [DataRow(WinGetPackageOperationKind.Uninstall, WinGetPackageOperationState.Succeeded)]
    [DataRow(WinGetPackageOperationKind.Uninstall, WinGetPackageOperationState.Failed)]
    [DataRow(WinGetPackageOperationKind.Uninstall, WinGetPackageOperationState.Canceled)]
    public void OtherSurfaceOperationsDoNotChangeFooterOrShowNotifications(WinGetPackageOperationKind kind, WinGetPackageOperationState state)
    {
        var operation = CreateOperation("Test.Gallery", "Gallery Extension", kind, WinGetPackageOperationSource.Unspecified);

        StartOperation(operation);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.IsEmpty(_notifications);

        CompleteOperation(operation, state);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.IsEmpty(_notifications);
    }

    [TestMethod]
    public void OtherSurfaceOperationForSamePackageDoesNotReplaceWinGetFeedback()
    {
        var winget = CreateOperation("Test.App", "WinGet App");
        var gallery = CreateOperation("Test.App", "Gallery Extension", source: WinGetPackageOperationSource.Unspecified);
        StartOperation(winget);

        StartOperation(gallery);

        Assert.AreEqual(Format(Resources.winget_installing_package, winget.PackageName), _page.Title);
        Assert.HasCount(1, _notifications);

        CompleteOperation(gallery, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(Format(Resources.winget_installing_package, winget.PackageName), _page.Title);
        Assert.HasCount(1, _notifications);

        CompleteOperation(winget, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.AreEqual(Format(Resources.winget_install_package_finished, winget.PackageName), _notifications[1]);
    }

    [TestMethod]
    public void MissingDisplayNameFallsBackToPackageId()
    {
        var operation = CreateOperation("Test.App", string.Empty);

        StartOperation(operation);

        Assert.AreEqual(Format(Resources.winget_installing_package, operation.PackageId), _page.Title);
        Assert.AreEqual(_page.Title, _notifications[0]);
    }

    [TestMethod]
    public void DisposeUnsubscribesAndIgnoresQueuedFeedback()
    {
        var operation = CreateOperation("Test.App", "Test App");
        _operations.Add(operation);
        _tracker.Raise(tracker => tracker.OperationStarted += null, new WinGetPackageOperationEventArgs(operation));

        _provider.Dispose();
        _scheduler.RunAll();
        CompleteOperation(operation, WinGetPackageOperationState.Succeeded);

        Assert.AreEqual(string.Empty, _page.Title);
        Assert.IsEmpty(_notifications);
        _tracker.VerifyRemove(tracker => tracker.OperationStarted -= It.IsAny<EventHandler<WinGetPackageOperationEventArgs>>(), Times.Once);
        _tracker.VerifyRemove(tracker => tracker.OperationCompleted -= It.IsAny<EventHandler<WinGetPackageOperationEventArgs>>(), Times.Once);
    }

    private void StartOperation(WinGetPackageOperation operation)
    {
        _operations.Insert(0, operation);
        _tracker.Raise(tracker => tracker.OperationStarted += null, new WinGetPackageOperationEventArgs(operation));
        _scheduler.RunAll();
    }

    private void CompleteOperation(WinGetPackageOperation operation, WinGetPackageOperationState state)
    {
        var completed = operation with { State = state, CompletedAt = DateTimeOffset.UtcNow };
        _operations[_operations.FindIndex(item => item.OperationId == operation.OperationId)] = completed;
        _tracker.Raise(tracker => tracker.OperationCompleted += null, new WinGetPackageOperationEventArgs(completed));
        _scheduler.RunAll();
    }

    private static string Format(string format, string packageName) => string.Format(CultureInfo.CurrentCulture, format, packageName);

    private static WinGetPackageOperation CreateOperation(
        string packageId,
        string packageName,
        WinGetPackageOperationKind kind = WinGetPackageOperationKind.Install,
        WinGetPackageOperationSource source = WinGetPackageOperationSource.WinGetExtension) =>
        new(
            OperationId: Guid.NewGuid(),
            PackageId: packageId,
            PackageName: packageName,
            Kind: kind,
            State: WinGetPackageOperationState.Queued,
            CanCancel: false,
            IsIndeterminate: true,
            ProgressPercent: null,
            BytesDownloaded: null,
            BytesRequired: null,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CompletedAt: null)
        {
            Source = source,
        };

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly Queue<Task> _tasks = [];

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                Assert.IsTrue(TryExecuteTask(task));
                task.GetAwaiter().GetResult();
            }
        }
    }
}
